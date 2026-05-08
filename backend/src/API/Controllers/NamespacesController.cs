using MediatR;
using Microsoft.AspNetCore.Mvc;
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
        var result = await mediator.Send(new ListNamespacesQuery(), cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<NamespaceResponse>> Create(
        [FromBody] CreateNamespaceRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            var actor = ActorSubject(httpContextAccessor);
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

    private static string ActorSubject(IHttpContextAccessor accessor) =>
        accessor.HttpContext?.Request.Headers.TryGetValue("X-Dev-User-Id", out var v) == true &&
        !string.IsNullOrWhiteSpace(v)
            ? v.ToString().Trim()
            : "anonymous";
}

public sealed record CreateNamespaceRequest(string Slug, string DisplayName, string? Description, string? Visibility);
