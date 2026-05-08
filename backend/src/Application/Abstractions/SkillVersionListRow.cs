namespace SkillRegistry.Application.Abstractions;

/// Version metadata for listings without loading stored ZIP bytes from Postgres.
public sealed record SkillVersionListRow(
    Guid Id,
    Guid PackageId,
    string Version,
    string? Tag,
    bool IsLatest,
    string ArtifactUri,
    DateTime PublishedAtUtc,
    bool HasStoredZip);
