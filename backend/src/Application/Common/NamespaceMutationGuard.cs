using SkillRegistry.Application.Abstractions;
using SkillRegistry.Domain.Entities;

namespace SkillRegistry.Application.Common;

/// <summary>
/// Resolves namespaces for mutations: missing slug → <see cref="KeyNotFoundException"/>;
/// present but actor cannot manage → <see cref="UnauthorizedAccessException"/> (403).
/// </summary>
public static class NamespaceMutationGuard
{
    public const string SkillManageDenied =
        "You don't have permission to manage skills in this namespace. Ask a namespace owner or admin for Admin access.";

    public const string NamespaceSettingsDenied =
        "You don't have permission to manage this namespace. Ask a namespace owner or admin for Admin access.";

    public static async Task<SkillNamespace> RequireForSkillMutationAsync(
        ISkillRegistryPersistence persistence,
        string normalizedSlug,
        string actorSubject,
        CancellationToken cancellationToken)
    {
        if (!await persistence.NamespaceSlugExistsAsync(normalizedSlug, cancellationToken).ConfigureAwait(false))
            throw new KeyNotFoundException($"Namespace '{normalizedSlug}' was not found.");

        return await persistence.GetNamespaceTrackedForMutationAsync(normalizedSlug, actorSubject, cancellationToken)
                   .ConfigureAwait(false)
               ?? throw new UnauthorizedAccessException(SkillManageDenied);
    }

    public static async Task<SkillNamespace> RequireForNamespaceMutationAsync(
        ISkillRegistryPersistence persistence,
        string normalizedSlug,
        string actorSubject,
        CancellationToken cancellationToken)
    {
        if (!await persistence.NamespaceSlugExistsAsync(normalizedSlug, cancellationToken).ConfigureAwait(false))
            throw new KeyNotFoundException($"Namespace '{normalizedSlug}' was not found.");

        return await persistence.GetNamespaceTrackedForMutationAsync(normalizedSlug, actorSubject, cancellationToken)
                   .ConfigureAwait(false)
               ?? throw new UnauthorizedAccessException(NamespaceSettingsDenied);
    }
}
