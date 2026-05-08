using MediatR;
using Microsoft.AspNetCore.Mvc;
using SkillRegistry.API;
using SkillRegistry.Application.SkillNamespaces;
using SkillRegistry.Domain.Enums;

namespace SkillRegistry.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class NamespacesController(IMediator mediator, IHttpContextAccessor httpContextAccessor) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NamespaceResponse>>> List(CancellationToken cancellationToken)
    {
        var viewer = RegistryActorResolver.Resolve(httpContextAccessor);
        var result = await mediator.Send(new ListNamespacesQuery(viewer), cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<NamespaceResponse>> Create(
        [FromBody] CreateNamespaceRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            var actor = RegistryActorResolver.Resolve(httpContextAccessor);
            var visibility = Enum.TryParse<NamespaceVisibility>(body.Visibility ?? "Private", ignoreCase: true, out var vis)
                ? vis
                : NamespaceVisibility.Private;

            var created = await mediator.Send(
                    new CreateNamespaceCommand(body.Slug, body.DisplayName, body.Description, visibility, actor),
                    cancellationToken)
                .ConfigureAwait(false);
            return CreatedAtAction(nameof(List), new { }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPut("{slug}")]
    public async Task<ActionResult<NamespaceResponse>> Update(
        string slug,
        [FromBody] UpdateNamespaceRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            var actor = RegistryActorResolver.Resolve(httpContextAccessor);
            var visibility = Enum.TryParse<NamespaceVisibility>(body.Visibility ?? "Private", ignoreCase: true, out var vis)
                ? vis
                : NamespaceVisibility.Private;

            var updated = await mediator.Send(
                    new UpdateNamespaceCommand(slug, body.DisplayName, body.Description, visibility, actor),
                    cancellationToken)
                .ConfigureAwait(false);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
    }

    [HttpDelete("{slug}")]
    public async Task<ActionResult> Delete(string slug, CancellationToken cancellationToken)
    {
        try
        {
            var actor = RegistryActorResolver.Resolve(httpContextAccessor);
            await mediator.Send(new DeleteNamespaceCommand(slug, actor), cancellationToken).ConfigureAwait(false);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
    }
}

public sealed record CreateNamespaceRequest(string Slug, string DisplayName, string? Description, string? Visibility);

public sealed record UpdateNamespaceRequest(string DisplayName, string? Description, string? Visibility);
