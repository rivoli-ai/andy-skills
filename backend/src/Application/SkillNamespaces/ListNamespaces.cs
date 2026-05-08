using MediatR;
using SkillRegistry.Application.Abstractions;

namespace SkillRegistry.Application.SkillNamespaces;

public sealed record ListNamespacesQuery : IRequest<IReadOnlyList<NamespaceResponse>>;

public sealed record NamespaceResponse(
    Guid Id,
    string Slug,
    string DisplayName,
    string? Description,
    string Visibility,
    DateTime CreatedAtUtc);

public sealed class ListNamespacesQueryHandler(ISkillRegistryPersistence persistence)
    : IRequestHandler<ListNamespacesQuery, IReadOnlyList<NamespaceResponse>>
{
    public async Task<IReadOnlyList<NamespaceResponse>> Handle(
        ListNamespacesQuery request,
        CancellationToken cancellationToken)
    {
        var list = await persistence.ListNamespacesAsync(cancellationToken);
        return list
            .OrderBy(n => n.Slug)
            .Select(n => new NamespaceResponse(
                n.Id,
                n.Slug,
                n.DisplayName,
                n.Description,
                n.Visibility.ToString(),
                n.CreatedAtUtc))
            .ToList();
    }
}
