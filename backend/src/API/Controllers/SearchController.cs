using MediatR;
using Microsoft.AspNetCore.Mvc;
using SkillRegistry.API;
using SkillRegistry.Application.SkillPackages;

namespace SkillRegistry.API.Controllers;

[ApiController]
[Route("api")]
public sealed class SearchController(IMediator mediator, IHttpContextAccessor httpContextAccessor) : ControllerBase
{
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<PackageSearchHitResponse>>> Search(
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        var viewer = RegistryActorResolver.Resolve(httpContextAccessor);
        var result = await mediator.Send(new SearchPackagesQuery(q, viewer), cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }
}
