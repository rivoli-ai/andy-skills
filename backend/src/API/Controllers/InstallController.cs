using MediatR;
using Microsoft.AspNetCore.Mvc;
using SkillRegistry.Application.SkillPackages;

namespace SkillRegistry.API.Controllers;

[ApiController]
[Route("api/install")]
public sealed class InstallController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Download the skill package ZIP for local installs (curl / wget / custom CLIs).
    /// When the package ZIP was stored in the registry database, streams those bytes; otherwise redirects to a stored HTTP(S) artifact URI.
    /// </summary>
    [HttpGet("{namespaceSlug}/{skillSlug}/{version}/package.zip")]
    public async Task<IActionResult> DownloadPackage(
        string namespaceSlug,
        string skillSlug,
        string version,
        CancellationToken cancellationToken)
    {
        var outcome = await mediator
            .Send(new ResolveSkillDownloadQuery(namespaceSlug, skillSlug, version), cancellationToken)
            .ConfigureAwait(false);

        return outcome switch
        {
            SkillDownloadZipBlob z => File(z.ZipBytes, "application/zip", $"{skillSlug}-{version}.zip"),
            SkillDownloadRedirect r => Redirect(r.Url.ToString()),
            _ => NotFound(),
        };
    }
}
