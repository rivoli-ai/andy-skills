using MediatR;
using Microsoft.AspNetCore.Mvc;
using SkillRegistry.Application.SkillPackages;

namespace SkillRegistry.API.Controllers;

[ApiController]
[Route("api")]
public sealed class SearchController(IMediator mediator) : ControllerBase
{
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<PackageSearchHitResponse>>> Search(
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SearchPackagesQuery(q), cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }
}
