using MediatR;
using SkillRegistry.Application.Abstractions;

namespace SkillRegistry.Application.SkillNamespaces;

public sealed record ListNamespacesQuery(string? ViewerSubject) : IRequest<IReadOnlyList<NamespaceResponse>>;

public sealed record NamespaceResponse(
    Guid Id,
    string Slug,
    string DisplayName,
    string? Description,
    string Visibility,
    DateTime CreatedAtUtc,
    string? CreatedBySubject);

public sealed class ListNamespacesQueryHandler(ISkillRegistryPersistence persistence)
    : IRequestHandler<ListNamespacesQuery, IReadOnlyList<NamespaceResponse>>
{
    public async Task<IReadOnlyList<NamespaceResponse>> Handle(
        ListNamespacesQuery request,
        CancellationToken cancellationToken)
    {
        var list = await persistence.ListNamespacesVisibleAsync(request.ViewerSubject, cancellationToken);
        return list
            .Select(n => new NamespaceResponse(
                n.Id,
                n.Slug,
                n.DisplayName,
                n.Description,
                n.Visibility.ToString(),
                n.CreatedAtUtc,
                n.CreatedBySubject))
            .ToList();
    }
}
