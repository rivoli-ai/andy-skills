using SkillRegistry.Application.Common;

namespace SkillRegistry.Application.Abstractions;

public interface IRemoteArtifactFetcher
{
    /// <summary>Downloads a ZIP using optional PAT-derived Authorization (publish ingest / install proxy).</summary>
    Task<byte[]?> DownloadZipAsync(string artifactUri, string pat, CancellationToken cancellationToken);

    /// <summary>
    /// Downloads from <see cref="NormalizedArtifactFetch.FetchUri"/>; then applies
    /// <see cref="NormalizedArtifactFetch.GitHubRepoRelativeDirectory"/> (GitHub zipball subtree) or
    /// <see cref="NormalizedArtifactFetch.ZipStripLeadingFolderPrefix"/> (e.g. Azure DevOps folder ZIP) when set.
    /// </summary>
    Task<byte[]?> DownloadZipAsync(NormalizedArtifactFetch fetch, string pat, CancellationToken cancellationToken);
}
