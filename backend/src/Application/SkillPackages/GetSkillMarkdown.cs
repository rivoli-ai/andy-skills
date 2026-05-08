using MediatR;
using SkillRegistry.Application.Abstractions;
using SkillRegistry.Application.Common;

namespace SkillRegistry.Application.SkillPackages;

public sealed record GetSkillMarkdownQuery(string NamespaceSlug, string SkillSlug, string Version)
    : IRequest<SkillMarkdownResult>;

public abstract record SkillMarkdownResult;

public sealed record SkillMarkdownFound(string Markdown) : SkillMarkdownResult;

public sealed record SkillMarkdownNotFound(string Reason) : SkillMarkdownResult;

public sealed class GetSkillMarkdownQueryHandler(ISkillRegistryPersistence persistence)
    : IRequestHandler<GetSkillMarkdownQuery, SkillMarkdownResult>
{
    public async Task<SkillMarkdownResult> Handle(GetSkillMarkdownQuery request, CancellationToken cancellationToken)
    {
        var ns = SlugRules.NormalizeSlug(request.NamespaceSlug);
        var skill = SlugRules.NormalizeSlug(request.SkillSlug);
        var ver = request.Version.Trim();

        if (string.IsNullOrEmpty(ver))
            return new SkillMarkdownNotFound("Version is required.");

        var entity = await persistence
            .GetVersionBySlugTripleAsync(ns, skill, ver, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null)
            return new SkillMarkdownNotFound("Version not found.");

        if (entity.PackageZip is not { Length: > 0 })
            return new SkillMarkdownNotFound(
                "This version has no ZIP in the registry (remote URI only). Upload a ZIP or pick a version stored here.");

        var markdown = SkillPackageMarkdownReader.TryReadSkillMarkdown(entity.PackageZip);
        if (markdown == null)
            return new SkillMarkdownNotFound("SKILL.md was not found inside the ZIP or the archive is invalid.");

        return new SkillMarkdownFound(markdown);
    }
}
