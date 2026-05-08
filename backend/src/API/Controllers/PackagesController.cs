using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SkillRegistry.API;
using SkillRegistry.Application.Common;
using SkillRegistry.Application.Options;
using SkillRegistry.Application.SkillPackages;

namespace SkillRegistry.API.Controllers;

[ApiController]
[Route("api/namespaces/{namespaceSlug}/packages")]
public sealed class PackagesController(
    IMediator mediator,
    IHttpContextAccessor httpContextAccessor,
    IOptionsSnapshot<SkillsStorageOptions> skillsStorageOptions)
    : ControllerBase
{
    private const long MaxZipUploadBytes = 104_857_600;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PackageSummaryResponse>>> List(
        string namespaceSlug,
        CancellationToken cancellationToken)
    {
        try
        {
            var viewer = RegistryActorResolver.Resolve(httpContextAccessor);
            var result = await mediator.Send(new ListPackagesQuery(namespaceSlug, viewer), cancellationToken)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("{skillSlug}/versions")]
    public async Task<ActionResult<IReadOnlyList<SkillVersionResponse>>> ListVersions(
        string namespaceSlug,
        string skillSlug,
        CancellationToken cancellationToken)
    {
        try
        {
            var viewer = RegistryActorResolver.Resolve(httpContextAccessor);
            var result = await mediator
                .Send(new ListSkillVersionsQuery(namespaceSlug, skillSlug, viewer), cancellationToken)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Extract <c>SKILL.md</c> from the ZIP stored in Postgres for this version (manager preview / copy for agents).
    /// </summary>
    [HttpGet("{skillSlug}/versions/{version}/SKILL.md")]
    public async Task<IActionResult> GetSkillMarkdown(
        string namespaceSlug,
        string skillSlug,
        string version,
        CancellationToken cancellationToken)
    {
        var viewer = RegistryActorResolver.Resolve(httpContextAccessor);
        var result = await mediator
            .Send(new GetSkillMarkdownQuery(namespaceSlug, skillSlug, version, viewer), cancellationToken)
            .ConfigureAwait(false);

        return result switch
        {
            SkillMarkdownFound f => Content(f.Markdown, "text/markdown; charset=utf-8"),
            SkillMarkdownNotFound n => NotFound(new { error = n.Reason }),
            _ => NotFound(),
        };
    }

    /// <summary>Lists file paths inside the stored ZIP for Explorer-style browsing.</summary>
    [HttpGet("{skillSlug}/versions/{version}/zip/tree")]
    public async Task<IActionResult> GetSkillZipTree(
        string namespaceSlug,
        string skillSlug,
        string version,
        CancellationToken cancellationToken)
    {
        var viewer = RegistryActorResolver.Resolve(httpContextAccessor);
        var result = await mediator
            .Send(new GetSkillZipTreeQuery(namespaceSlug, skillSlug, version, viewer), cancellationToken)
            .ConfigureAwait(false);

        return result switch
        {
            SkillZipTreeFound f => Ok(new { paths = f.Paths }),
            SkillZipTreeNotFound n => NotFound(new { error = n.Reason }),
            _ => NotFound(),
        };
    }

    /// <summary>Reads a text preview (or marks binary) for one path inside the stored ZIP.</summary>
    [HttpGet("{skillSlug}/versions/{version}/zip/file")]
    public async Task<IActionResult> GetSkillZipFile(
        string namespaceSlug,
        string skillSlug,
        string version,
        [FromQuery] string path,
        CancellationToken cancellationToken)
    {
        var viewer = RegistryActorResolver.Resolve(httpContextAccessor);
        var result = await mediator
            .Send(new GetSkillZipFileQuery(namespaceSlug, skillSlug, version, path, viewer), cancellationToken)
            .ConfigureAwait(false);

        return result switch
        {
            SkillZipFileFound f => Ok(new
            {
                path = f.Path,
                content = f.ContentUtf8,
                isBinary = f.IsBinary,
                sizeBytes = f.SizeBytes,
                truncated = f.SizeBytes > SkillPackageZipBrowser.MaxPreviewBytes,
            }),
            SkillZipFileNotFound n => NotFound(new { error = n.Reason }),
            _ => NotFound(),
        };
    }

    [HttpPost]
    public async Task<ActionResult<PackageSummaryResponse>> Create(
        string namespaceSlug,
        [FromBody] CreateSkillPackageRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            var actor = RegistryActorResolver.Resolve(httpContextAccessor);
            var created = await mediator.Send(
                    new CreateSkillPackageCommand(
                        namespaceSlug,
                        body.Slug,
                        body.Title,
                        body.Description,
                        actor),
                    cancellationToken)
                .ConfigureAwait(false);
            return CreatedAtAction(nameof(List), new { namespaceSlug }, created);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
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

    [HttpPost("{skillSlug}/versions")]
    public async Task<ActionResult<SkillVersionResponse>> PublishVersion(
        string namespaceSlug,
        string skillSlug,
        [FromBody] PublishSkillVersionRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            var actor = RegistryActorResolver.Resolve(httpContextAccessor);
            var created = await mediator.Send(
                    new PublishSkillVersionCommand(
                        namespaceSlug,
                        skillSlug,
                        body.Version,
                        body.Tag,
                        body.ArtifactUri,
                        actor,
                        body.PublisherPatOneTime),
                    cancellationToken)
                .ConfigureAwait(false);
            return Ok(created);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
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

    [HttpPut("{skillSlug}")]
    public async Task<ActionResult<PackageSummaryResponse>> Update(
        string namespaceSlug,
        string skillSlug,
        [FromBody] UpdateSkillPackageRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            var actor = RegistryActorResolver.Resolve(httpContextAccessor);
            var updated = await mediator
                .Send(
                    new UpdateSkillPackageCommand(
                        namespaceSlug,
                        skillSlug,
                        body.Title,
                        body.Description,
                        actor),
                    cancellationToken)
                .ConfigureAwait(false);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{skillSlug}")]
    public async Task<ActionResult> Delete(
        string namespaceSlug,
        string skillSlug,
        CancellationToken cancellationToken)
    {
        try
        {
            var actor = RegistryActorResolver.Resolve(httpContextAccessor);
            await mediator
                .Send(new DeleteSkillPackageCommand(namespaceSlug, skillSlug, actor), cancellationToken)
                .ConfigureAwait(false);
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

    /// <summary>
    /// Upload a ZIP for this skill version; persists bytes on the version row (<c>PackageZip</c>, Postgres <c>bytea</c>)
    /// and publishes with <c>artifactUri</c> set to the public install URL for download.
    /// </summary>
    [HttpPost("{skillSlug}/versions/upload")]
    [RequestSizeLimit(MaxZipUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxZipUploadBytes)]
    public async Task<ActionResult<SkillVersionResponse>> UploadZipVersion(
        string namespaceSlug,
        string skillSlug,
        [FromForm] string version,
        [FromForm] string? tag,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "A non-empty ZIP file is required." });

        if (file.Length > MaxZipUploadBytes)
            return BadRequest(new { error = "ZIP exceeds the maximum upload size (100 MB)." });

        var nsNorm = SlugRules.NormalizeSlug(namespaceSlug);
        var skillNorm = SlugRules.NormalizeSlug(skillSlug);
        var verNorm = version.Trim();
        if (string.IsNullOrWhiteSpace(verNorm) || verNorm.Length > 64)
            return BadRequest(new { error = "Invalid version string." });

        if (verNorm.AsSpan().IndexOfAny(['/', '\\']) >= 0)
            return BadRequest(new { error = "Version cannot contain path separators." });

        var tagNorm = string.IsNullOrWhiteSpace(tag) ? null : tag.Trim();

        await using var ms = new MemoryStream((int)Math.Min(file.Length, MaxZipUploadBytes));
        await using (var upload = file.OpenReadStream())
        {
            await upload.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        }

        if (ms.Length < 4)
            return BadRequest(new { error = "Uploaded file is too small to be a ZIP archive." });

        ms.Position = 0;
        var sig = new byte[4];
        var read = await ms.ReadAsync(sig.AsMemory(0, 4), cancellationToken).ConfigureAwait(false);
        if (read < 4 || sig[0] != (byte)'P' || sig[1] != (byte)'K')
            return BadRequest(new { error = "File does not look like a ZIP archive." });

        ms.Position = 0;

        var opts = skillsStorageOptions.Value;
        var installUri = BuildInstallZipUrl(opts, nsNorm, skillNorm, verNorm);

        try
        {
            var zipBytes = ms.ToArray();
            var actor = RegistryActorResolver.Resolve(httpContextAccessor);
            var created = await mediator
                .Send(
                    new PublishSkillVersionCommand(
                        nsNorm,
                        skillNorm,
                        verNorm,
                        tagNorm,
                        installUri,
                        actor,
                        PackageZip: zipBytes),
                    cancellationToken)
                .ConfigureAwait(false);
            return Ok(created);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
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

    private static string BuildInstallZipUrl(SkillsStorageOptions opts, string ns, string skill, string version)
    {
        var baseUrl = opts.PublicBaseUrl.TrimEnd('/');
        return $"{baseUrl}/api/install/{Uri.EscapeDataString(ns)}/{Uri.EscapeDataString(skill)}/{Uri.EscapeDataString(version)}/package.zip";
    }
}

public sealed record CreateSkillPackageRequest(string Slug, string Title, string? Description);

public sealed record UpdateSkillPackageRequest(string Title, string? Description);

public sealed record PublishSkillVersionRequest(
    string Version,
    string? Tag,
    string ArtifactUri,
    string? PublisherPatOneTime = null);
