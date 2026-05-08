namespace SkillRegistry.Application.Common;

/// <summary>Maps common GitHub / Azure DevOps browser URLs to HTTP ZIP endpoints the fetcher can call.</summary>
public static class ArtifactUriNormalizer
{
    public static string NormalizeArtifactUri(string input)
    {
        var t = input.Trim();
        return TryNormalizeToFetchUri(t, out var n, out _, out _) ? n : t;
    }

    /// <summary>Resolves a user-entered URL into a fetch URI and optional ZIP rewriting hints.</summary>
    public static NormalizedArtifactFetch ResolveFetch(string input)
    {
        var t = input.Trim();
        return TryNormalizeToFetchUri(t, out var uri, out var ghDir, out var zipStrip)
            ? new NormalizedArtifactFetch(uri, NormalizeGhSubdir(ghDir), NormalizeGhSubdir(zipStrip))
            : new NormalizedArtifactFetch(t, null);
    }

    public static bool TryNormalizeToFetchUri(string input, out string normalized)
    {
        return TryNormalizeToFetchUri(input, out normalized, out _, out _);
    }

    public static bool TryNormalizeToFetchUri(string input, out string normalized, out string? githubRepoRelativeDirectory)
    {
        return TryNormalizeToFetchUri(input, out normalized, out githubRepoRelativeDirectory, out _);
    }

    public static bool TryNormalizeToFetchUri(
        string input,
        out string normalized,
        out string? githubRepoRelativeDirectory,
        out string? zipStripLeadingFolderPrefix)
    {
        githubRepoRelativeDirectory = null;
        zipStripLeadingFolderPrefix = null;
        normalized = input.Trim();
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            return false;

        if (uri.AbsolutePath.Contains("_apis/git", StringComparison.OrdinalIgnoreCase))
            return false;

        var host = uri.IdnHost;
        if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            host = host[4..];

        if (string.Equals(host, "github.com", StringComparison.OrdinalIgnoreCase))
            return TryGitHubWebToZipball(uri, out normalized, out githubRepoRelativeDirectory);

        if (string.Equals(host, "dev.azure.com", StringComparison.OrdinalIgnoreCase))
            return TryAzureDevOpsWebToItemsZip(uri, out normalized, out zipStripLeadingFolderPrefix);

        if (host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase))
            return TryVisualStudioHostToItemsZip(uri, out normalized, out zipStripLeadingFolderPrefix);

        return false;
    }

    private static string? NormalizeGhSubdir(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir))
            return null;
        var t = dir.Trim().Trim('/').Replace('\\', '/');
        return t.Length == 0 ? null : t;
    }

    private static bool TryGitHubWebToZipball(Uri uri, out string normalized, out string? repoRelativeDirectory)
    {
        repoRelativeDirectory = null;
        normalized = uri.ToString();
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
            return false;

        var owner = segments[0];
        var repo = segments[1];
        if (repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            repo = repo[..^4];

        var refName = "main";
        if (segments.Length >= 4 && segments[2].Equals("tree", StringComparison.OrdinalIgnoreCase))
        {
            refName = segments[3];
            if (segments.Length > 4)
                repoRelativeDirectory = string.Join("/", segments.Skip(4));
        }
        else if (segments.Length >= 5 && segments[2].Equals("blob", StringComparison.OrdinalIgnoreCase))
        {
            refName = segments[3];
            var blobPath = string.Join("/", segments.Skip(4));
            var slash = blobPath.LastIndexOf('/');
            repoRelativeDirectory = slash < 0 ? null : blobPath[..slash];
        }

        normalized =
            $"https://api.github.com/repos/{owner}/{repo}/zipball/{Uri.EscapeDataString(refName)}";
        return true;
    }

    private static bool TryAzureDevOpsWebToItemsZip(
        Uri uri,
        out string normalized,
        out string? zipStripLeadingFolderPrefix)
    {
        zipStripLeadingFolderPrefix = null;
        normalized = uri.ToString();
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var gitIdx = Array.FindIndex(segments, s => s.Equals("_git", StringComparison.OrdinalIgnoreCase));
        if (gitIdx < 2 || gitIdx + 1 >= segments.Length)
            return false;

        var org = segments[0];
        var project = segments[1];
        var repo = segments[gitIdx + 1];
        if (!TryNormalizeAzureRepoPath(TryGetDecodedQueryValue(uri, "path"), out var itemPath))
            return false;

        if (!itemPath.Equals("/", StringComparison.Ordinal))
            zipStripLeadingFolderPrefix = itemPath.Trim('/').Replace('\\', '/');

        normalized = BuildAzureGitItemsZipUrl(
            $"https://dev.azure.com/{EncodeUriSegment(org)}/{EncodeUriSegment(project)}",
            repo,
            itemPath,
            uri);
        return true;
    }

    private static bool TryVisualStudioHostToItemsZip(
        Uri uri,
        out string normalized,
        out string? zipStripLeadingFolderPrefix)
    {
        zipStripLeadingFolderPrefix = null;
        normalized = uri.ToString();
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var gitIdx = Array.FindIndex(segments, s => s.Equals("_git", StringComparison.OrdinalIgnoreCase));
        if (gitIdx < 1 || gitIdx + 1 >= segments.Length)
            return false;

        var project = segments[0];
        var repo = segments[gitIdx + 1];
        var origin = uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        if (!TryNormalizeAzureRepoPath(TryGetDecodedQueryValue(uri, "path"), out var itemPath))
            return false;

        if (!itemPath.Equals("/", StringComparison.Ordinal))
            zipStripLeadingFolderPrefix = itemPath.Trim('/').Replace('\\', '/');

        normalized = BuildAzureGitItemsZipUrl($"{origin}/{EncodeUriSegment(project)}", repo, itemPath, uri);
        return true;
    }

    /// <summary>
    /// Builds Azure Git Items ZIP URL. Folders require <c>scopePath</c> (not <c>path</c>) with <c>recursionLevel=full</c>.
    /// ZIP payload requires OData <c>$format=zip</c> (<c>%24format=zip</c>); plain <c>format=zip</c> returns JSON listings.
    /// See <see href="https://learn.microsoft.com/en-us/rest/api/azure/devops/git/items/get">Items — Get</see>.
    /// </summary>
    private static string BuildAzureGitItemsZipUrl(string originThroughProject, string repo, string path, Uri originalWebUri)
    {
        var scopeEncoded = Uri.EscapeDataString(path);
        var q =
            $"scopePath={scopeEncoded}&recursionLevel=full&api-version=7.1&%24format=zip&download=true"
            + TryAzureVersionDescriptorQuery(originalWebUri);
        return $"{originThroughProject}/_apis/git/repositories/{EncodeUriSegment(repo)}/items?{q}";
    }

    /// <summary>Maps Azure DevOps web <c>version=GBbranch</c> to REST <c>versionDescriptor</c> query pairs.</summary>
    private static string TryAzureVersionDescriptorQuery(Uri uri)
    {
        var version = TryGetDecodedQueryValue(uri, "version");
        if (string.IsNullOrWhiteSpace(version))
            return "";

        version = version.Trim();
        if (!version.StartsWith("GB", StringComparison.OrdinalIgnoreCase))
            return "";

        var branch = version[2..].Trim();
        if (branch.Length == 0 || branch.Contains("..", StringComparison.Ordinal))
            return "";

        return $"&versionDescriptor.version={Uri.EscapeDataString(branch)}&versionDescriptor.versionType=branch";
    }

    private static bool TryNormalizeAzureRepoPath(string? pathFromQuery, out string normalizedPath)
    {
        normalizedPath = "/";
        if (string.IsNullOrWhiteSpace(pathFromQuery))
            return true;

        var p = pathFromQuery.Trim().Replace('\\', '/');
        if (p.Contains("..", StringComparison.Ordinal))
            return false;

        if (!p.StartsWith('/'))
            p = "/" + p;

        normalizedPath = p;
        return true;
    }

    private static string EncodeUriSegment(string segment) =>
        Uri.EscapeDataString(segment);

    private static string? TryGetDecodedQueryValue(Uri uri, string key)
    {
        var query = uri.Query;
        if (string.IsNullOrEmpty(query))
            return null;

        ReadOnlySpan<char> span = query.AsSpan().TrimStart('?');
        while (!span.IsEmpty)
        {
            ReadOnlySpan<char> pair;
            var amp = span.IndexOf('&');
            if (amp >= 0)
            {
                pair = span[..amp];
                span = span[(amp + 1)..];
            }
            else
            {
                pair = span;
                span = ReadOnlySpan<char>.Empty;
            }

            var eq = pair.IndexOf('=');
            if (eq <= 0)
                continue;

            var name = Uri.UnescapeDataString(pair[..eq].ToString());
            if (!name.Equals(key, StringComparison.OrdinalIgnoreCase))
                continue;

            var value = eq + 1 < pair.Length ? pair[(eq + 1)..].ToString() : "";
            return Uri.UnescapeDataString(value);
        }

        return null;
    }
}
