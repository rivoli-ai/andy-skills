using SkillRegistry.Domain.Entities;
using SkillRegistry.Domain.Enums;

namespace SkillRegistry.Application.Common;

/// <summary>
/// Org-wide namespaces (<see cref="NamespaceVisibility.OrgVisible"/>) are visible to everyone.
/// Private namespaces are visible only to users listed in <see cref="SkillNamespace.Members"/>.
/// Mutations require <see cref="NamespaceRole.Owner"/> or <see cref="NamespaceRole.Admin"/>.
/// Unauthenticated callers cannot mutate (subject normalizes to null).
/// </summary>
public static class NamespaceAccess
{
    public static string? NormalizeSubject(string? subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
            return null;
        var t = subject.Trim();
        if (t.Equals("anonymous", StringComparison.OrdinalIgnoreCase))
            return null;
        return t;
    }

    private static bool SubjectMatchesMember(string actorNorm, string memberSubjectId) =>
        string.Equals(actorNorm, memberSubjectId.Trim(), StringComparison.OrdinalIgnoreCase);

    public static bool CanView(SkillNamespace ns, string? viewerSubject)
    {
        ArgumentNullException.ThrowIfNull(ns);
        if (ns.Visibility == NamespaceVisibility.OrgVisible)
            return true;

        var v = NormalizeSubject(viewerSubject);
        if (v == null)
            return false;

        return ns.Members.Any(m => SubjectMatchesMember(v, m.SubjectUserId));
    }

    public static bool CanManage(SkillNamespace ns, string? actorSubject)
    {
        ArgumentNullException.ThrowIfNull(ns);
        var a = NormalizeSubject(actorSubject);
        if (a == null)
            return false;

        return ns.Members.Any(m =>
            SubjectMatchesMember(a, m.SubjectUserId)
            && (m.Role == NamespaceRole.Owner || m.Role == NamespaceRole.Admin));
    }
}
