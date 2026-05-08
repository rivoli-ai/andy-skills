using System.IO.Compression;
using System.Text;

namespace SkillRegistry.Application.SkillPackages;

/// <summary>Lists and reads text previews from an in-memory skill ZIP (same store as SKILL.md extraction).</summary>
public static class SkillPackageZipBrowser
{
    public const int MaxPreviewBytes = 524_288;

    public static bool TryNormalizeZipEntryPath(string entryFullName, out string normalizedPath)
    {
        normalizedPath = "";
        if (string.IsNullOrWhiteSpace(entryFullName))
            return false;

        var s = entryFullName.Replace('\\', '/').Trim();
        if (s.EndsWith("/", StringComparison.Ordinal))
            return false;

        var parts = s.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            if (p is "." or "..")
                return false;
        }

        normalizedPath = string.Join("/", parts);
        return normalizedPath.Length > 0;
    }

    public static bool TryNormalizeClientPath(string? path, out string normalizedPath, out string? error)
    {
        normalizedPath = "";
        error = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Path is required.";
            return false;
        }

        var s = path.Replace('\\', '/').Trim().TrimStart('/');
        var parts = s.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            if (p is "." or "..")
            {
                error = "Invalid path.";
                return false;
            }
        }

        normalizedPath = string.Join("/", parts);
        if (normalizedPath.Length == 0)
        {
            error = "Invalid path.";
            return false;
        }

        return true;
    }

    public static IReadOnlyList<string> ListPaths(byte[] zipBytes)
    {
        if (zipBytes.Length == 0)
            return [];

        try
        {
            using var ms = new MemoryStream(zipBytes, writable: false);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in zip.Entries)
            {
                if (!TryNormalizeZipEntryPath(entry.FullName, out var np))
                    continue;
                set.Add(np);
            }

            var list = set.ToList();
            list.Sort(StringComparer.Ordinal);
            return list;
        }
        catch (InvalidDataException)
        {
            return [];
        }
    }

    public sealed record ZipEntryPreview(bool Found, bool IsBinary, string ContentUtf8, long TotalUncompressedBytes, string? Error);

    /// <summary>Reads up to <see cref="MaxPreviewBytes"/> for preview; detects NUL bytes as binary.</summary>
    public static ZipEntryPreview TryReadEntryPreview(byte[] zipBytes, string normalizedRelativePath)
    {
        if (zipBytes.Length == 0)
            return new ZipEntryPreview(false, false, "", 0, "Archive is empty.");

        try
        {
            using var ms = new MemoryStream(zipBytes, writable: false);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            ZipArchiveEntry? match = null;
            foreach (var entry in zip.Entries)
            {
                if (!TryNormalizeZipEntryPath(entry.FullName, out var np))
                    continue;
                if (string.Equals(np, normalizedRelativePath, StringComparison.OrdinalIgnoreCase))
                {
                    match = entry;
                    break;
                }
            }

            if (match == null)
                return new ZipEntryPreview(false, false, "", 0, "File not found in archive.");

            var totalLen = match.Length;
            var cap = (int)Math.Min(totalLen, MaxPreviewBytes);
            if (cap == 0)
                return new ZipEntryPreview(true, false, "", totalLen, null);

            var buffer = new byte[cap];
            using (var stream = match.Open())
            {
                var read = stream.ReadAtLeast(buffer, cap, throwOnEndOfStream: false);
                if (read < buffer.Length)
                    Array.Resize(ref buffer, read);
            }

            if (buffer.AsSpan().IndexOf((byte)0) >= 0)
                return new ZipEntryPreview(true, true, "", totalLen, null);

            var text = Encoding.UTF8.GetString(buffer);
            return new ZipEntryPreview(true, false, text, totalLen, null);
        }
        catch (InvalidDataException ex)
        {
            return new ZipEntryPreview(false, false, "", 0, ex.Message);
        }
    }
}
