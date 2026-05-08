using System.Text;

namespace SkillRegistry.Application.Common;

public static class ArtifactAuthorizationBuilder
{
    /// <summary>Builds Authorization header value for GitHub (Bearer) or Azure DevOps (Basic :pat).</summary>
    public static string? Build(string artifactUri, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var t = token.Trim();
        if (!Uri.TryCreate(artifactUri, UriKind.Absolute, out var uri))
            return $"Bearer {t}";

        var host = uri.IdnHost;
        if (host.Contains("github.com", StringComparison.OrdinalIgnoreCase))
            return $"Bearer {t}";

        if (host.Contains("dev.azure.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase))
        {
            var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes(":" + t));
            return $"Basic {basic}";
        }

        return $"Bearer {t}";
    }
}
