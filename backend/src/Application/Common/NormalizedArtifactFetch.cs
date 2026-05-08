namespace SkillRegistry.Application.Common;

/// <param name="FetchUri">HTTP(S) URL whose response body should be a ZIP (after redirects).</param>
/// <param name="GitHubRepoRelativeDirectory">
/// When set, the ZIP is a GitHub <c>zipball</c> for a branch/tag; keep only this path relative to the repo root
/// (strip the single top-level folder GitHub adds, then take this subtree as the new archive root).
/// </param>
/// <param name="ZipStripLeadingFolderPrefix">
/// When set (typically Azure DevOps <c>items</c> scoped to a repo folder), strip this repo-relative path prefix from ZIP entries
/// so skill files sit at the archive root (matches scoped GitHub folder ingest).
/// </param>
public sealed record NormalizedArtifactFetch(
    string FetchUri,
    string? GitHubRepoRelativeDirectory,
    string? ZipStripLeadingFolderPrefix = null);
