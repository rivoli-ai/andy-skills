using System.IO.Compression;
using System.Net.Http;
using SkillRegistry.Application.Abstractions;
using SkillRegistry.Application.Common;

namespace SkillRegistry.Infrastructure.Services;

public sealed class RemoteArtifactFetcher(HttpClient http) : IRemoteArtifactFetcher
{
    public const long MaxArtifactBytes = 104_857_600;

    public async Task<byte[]?> DownloadZipAsync(string artifactUri, string pat, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(artifactUri, UriKind.Absolute, out var startUri))
            return null;

        try
        {
            Func<Uri, string?> authorize =
                target => ArtifactAuthorizationBuilder.Build(target.ToString(), pat);

            var bytes = await DownloadZipFollowingRedirects(startUri, authorize, cancellationToken).ConfigureAwait(false);
            if (bytes != null)
                return bytes;

            var trimmedPat = pat?.Trim();
            if (!string.IsNullOrEmpty(trimmedPat) && HostLooksLikeAzureDevOpsFamily(startUri))
            {
                var bearer = $"Bearer {trimmedPat}";
                authorize = _ => bearer;
                bytes = await DownloadZipFollowingRedirects(startUri, authorize, cancellationToken).ConfigureAwait(false);
            }

            return bytes;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// HttpClient is configured with <see cref="SocketsHttpHandler.AllowAutoRedirect"/> disabled so Authorization survives cross-host redirects
    /// (e.g. GitHub api.github.com → codeload.github.com). Azure SSO redirects are aborted so PAT failures surface cleanly or Bearer retry can run.
    /// </summary>
    private async Task<byte[]?> DownloadZipFollowingRedirects(
        Uri startUri,
        Func<Uri, string?> authorizationForTargetUri,
        CancellationToken cancellationToken)
    {
        var requestUri = startUri;
        for (var hop = 0; hop < 16; hop++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, requestUri);
            var auth = authorizationForTargetUri(requestUri);
            if (!string.IsNullOrEmpty(auth))
                req.Headers.TryAddWithoutValidation("Authorization", auth);
            req.Headers.TryAddWithoutValidation("User-Agent", "SkillRegistry-Artifact/1.0");

            using var resp =
                await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            var status = (int)resp.StatusCode;
            if (status >= 300 && status < 400)
            {
                var location = resp.Headers.Location;
                if (location == null)
                    return null;

                var next = location.IsAbsoluteUri ? location : new Uri(requestUri, location);
                if (IsAzureDevOpsAuthPortalRedirect(next))
                    return null;

                requestUri = next;
                continue;
            }

            if (!resp.IsSuccessStatusCode)
                return null;

            await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var ms = new MemoryStream();
            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) >
                   0)
            {
                total += read;
                if (total > MaxArtifactBytes)
                    return null;
                ms.Write(buffer, 0, read);
            }

            var body = ms.ToArray();
            if (body.Length < 4 || body[0] != (byte)'P' || body[1] != (byte)'K')
                return null;

            return body;
        }

        return null;
    }

    private static bool HostLooksLikeAzureDevOpsFamily(Uri uri)
    {
        var h = uri.IdnHost;
        return h.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase)
               || h.EndsWith(".dev.azure.com", StringComparison.OrdinalIgnoreCase)
               || h.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAzureDevOpsAuthPortalRedirect(Uri uri)
    {
        var h = uri.IdnHost;
        if (h.Contains("login.microsoftonline.com", StringComparison.OrdinalIgnoreCase))
            return true;
        if (h.Contains("microsoftonline.com", StringComparison.OrdinalIgnoreCase))
            return true;
        if (h.Contains("vssps.visualstudio.com", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    /// <inheritdoc />
    public async Task<byte[]?> DownloadZipAsync(NormalizedArtifactFetch fetch, string pat, CancellationToken cancellationToken)
    {
        var bytes = await DownloadZipAsync(fetch.FetchUri, pat, cancellationToken).ConfigureAwait(false);
        if (bytes == null)
            return null;

        if (!string.IsNullOrWhiteSpace(fetch.GitHubRepoRelativeDirectory))
        {
            var trimmed = fetch.GitHubRepoRelativeDirectory.Trim().Trim('/').Replace('\\', '/');
            if (trimmed.Length == 0 || trimmed.Contains("..", StringComparison.Ordinal))
                return null;

            return ExtractGithubRepoSubfolderZip(bytes, trimmed);
        }

        if (!string.IsNullOrWhiteSpace(fetch.ZipStripLeadingFolderPrefix))
        {
            var prefix = fetch.ZipStripLeadingFolderPrefix.Trim().Trim('/').Replace('\\', '/');
            if (prefix.Length > 0 && !prefix.Contains("..", StringComparison.Ordinal))
                bytes = StripLeadingFolderPrefixFromZip(bytes, prefix);
        }

        return bytes;
    }

    /// <summary>
    /// GitHub zipballs contain one root folder <c>{repo}-{sha}</c>; strip it, then take <paramref name="repoRelativeDir"/> as the new ZIP root.
    /// </summary>
    private static byte[]? ExtractGithubRepoSubfolderZip(byte[] zipBytes, string repoRelativeDir)
    {
        using var input = new MemoryStream(zipBytes);
        using var zip = new ZipArchive(input, ZipArchiveMode.Read);

        var rootPrefix = GetGithubZipRootPrefix(zip);
        if (rootPrefix == null)
            return null;

        var folderPrefix = repoRelativeDir + "/";
        using var output = new MemoryStream();
        using (var outZip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var matched = 0;
            foreach (var entry in zip.Entries)
            {
                var full = entry.FullName.Replace('\\', '/');
                if (!full.StartsWith(rootPrefix, StringComparison.Ordinal))
                    continue;

                var repoRel = full[rootPrefix.Length..].TrimEnd('/');
                if (repoRel.Length == 0)
                    continue;

                if (!repoRel.Equals(repoRelativeDir, StringComparison.Ordinal)
                    && !repoRel.StartsWith(folderPrefix, StringComparison.Ordinal))
                    continue;

                var innerPath = repoRel.Equals(repoRelativeDir, StringComparison.Ordinal)
                    ? ""
                    : repoRel[folderPrefix.Length..];

                if (string.IsNullOrEmpty(entry.Name))
                    continue;

                if (string.IsNullOrEmpty(innerPath))
                    continue;

                if (innerPath.Contains("..", StringComparison.Ordinal))
                    continue;

                matched++;
                var created = outZip.CreateEntry(innerPath.Replace('\\', '/'));
                using var src = entry.Open();
                using var dst = created.Open();
                src.CopyTo(dst);
            }

            if (matched == 0)
                return null;
        }

        var result = output.ToArray();
        if (result.Length > MaxArtifactBytes || result.Length < 4 || result[0] != (byte)'P' || result[1] != (byte)'K')
            return null;
        return result;
    }

    /// <summary>
    /// When Azure (or similar) returns a ZIP whose entries all sit under <paramref name="folderPrefix"/> relative to the repo,
    /// rebuild a ZIP whose root is that folder’s contents (same goal as GitHub subtree ingest).
    /// </summary>
    private static byte[] StripLeadingFolderPrefixFromZip(byte[] zipBytes, string folderPrefix)
    {
        var folderOnly = folderPrefix.Trim().Trim('/').Replace('\\', '/');
        if (folderOnly.Length == 0 || folderOnly.Contains("..", StringComparison.Ordinal))
            return zipBytes;

        var slashPrefix = folderOnly + "/";

        using var input = new MemoryStream(zipBytes);
        using var zip = new ZipArchive(input, ZipArchiveMode.Read);

        bool UnderPrefix(string p) =>
            p.Equals(folderOnly, StringComparison.OrdinalIgnoreCase)
            || p.StartsWith(slashPrefix, StringComparison.OrdinalIgnoreCase);

        var paths = zip.Entries
            .Where(e => !string.IsNullOrEmpty(e.Name))
            .Select(e => e.FullName.Replace('\\', '/').TrimEnd('/'))
            .ToList();

        if (paths.Count == 0)
            return zipBytes;

        if (!paths.Any(UnderPrefix))
            return zipBytes;

        if (!paths.All(UnderPrefix))
            return zipBytes;

        using var output = new MemoryStream();
        using (var outZip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var matched = 0;
            foreach (var entry in zip.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                    continue;

                var full = entry.FullName.Replace('\\', '/').TrimEnd('/');
                if (!UnderPrefix(full))
                    continue;

                var innerPath = full.Equals(folderOnly, StringComparison.OrdinalIgnoreCase)
                    ? ""
                    : full[slashPrefix.Length..];

                if (string.IsNullOrEmpty(innerPath))
                    continue;

                if (innerPath.Contains("..", StringComparison.Ordinal))
                    continue;

                matched++;
                var created = outZip.CreateEntry(innerPath.Replace('\\', '/'));
                using var src = entry.Open();
                using var dst = created.Open();
                src.CopyTo(dst);
            }

            if (matched == 0)
                return zipBytes;
        }

        var result = output.ToArray();
        if (result.Length > MaxArtifactBytes || result.Length < 4 || result[0] != (byte)'P' || result[1] != (byte)'K')
            return zipBytes;
        return result;
    }

    private static string? GetGithubZipRootPrefix(ZipArchive zip)
    {
        foreach (var entry in zip.Entries)
        {
            var full = entry.FullName.Replace('\\', '/');
            var slash = full.IndexOf('/');
            if (slash > 0)
                return full[..(slash + 1)];
        }

        return null;
    }
}
