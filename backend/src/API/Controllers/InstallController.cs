using MediatR;
using Microsoft.AspNetCore.Mvc;
using SkillRegistry.API;
using SkillRegistry.Application.SkillPackages;

namespace SkillRegistry.API.Controllers;

[ApiController]
[Route("api/install")]
public sealed class InstallController(IMediator mediator, IHttpContextAccessor httpContextAccessor) : ControllerBase
{
    private const string ArtifactPatHeader = "X-Artifact-PAT";

    /// <summary>
    /// Download the skill package ZIP for local installs (curl / wget / custom CLIs).
    /// When the package ZIP was stored in the registry database, streams those bytes;
    /// when <see cref="SkillDownloadRedirect"/>, redirects to the HTTP(S) artifact URI;
    /// when the version is marked private-remote, requires each installer to send <c>X-Artifact-PAT</c> (never stored).
    /// </summary>
    [HttpGet("{namespaceSlug}/{skillSlug}/{version}/package.zip")]
    public async Task<IActionResult> DownloadPackage(
        string namespaceSlug,
        string skillSlug,
        string version,
        CancellationToken cancellationToken)
    {
        var viewer = RegistryActorResolver.Resolve(httpContextAccessor);
        var installerPat = Request.Headers.TryGetValue(ArtifactPatHeader, out var hv)
            ? hv.FirstOrDefault()
            : null;

        var outcome = await mediator
            .Send(
                new ResolveSkillDownloadQuery(namespaceSlug, skillSlug, version, viewer, installerPat),
                cancellationToken)
            .ConfigureAwait(false);

        return outcome switch
        {
            SkillDownloadZipBlob z => File(z.ZipBytes, "application/zip", $"{skillSlug}-{version}.zip"),
            SkillDownloadRedirect r => Redirect(r.Url.ToString()),
            SkillDownloadPatRequired => Unauthorized(new
            {
                error =
                    "This version uses a private GitHub or Azure DevOps ZIP URL. Send your personal token on the X-Artifact-PAT header for this request only (the registry never stores PATs). In this web app, save your token under Settings.",
                code = "artifact_pat_required",
            }),
            SkillDownloadNotFound => NotFound(),
            _ => NotFound(),
        };
    }
}
