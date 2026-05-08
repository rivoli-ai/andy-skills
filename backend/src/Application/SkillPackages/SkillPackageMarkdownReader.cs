using System.IO.Compression;
using System.Text;

namespace SkillRegistry.Application.SkillPackages;

public static class SkillPackageMarkdownReader
{
    /// <summary>Finds the shallowest SKILL.md entry (case-insensitive) inside a zip archive.</summary>
    public static string? TryReadSkillMarkdown(byte[] zipBytes)
    {
        if (zipBytes.Length == 0)
            return null;

        try
        {
            using var ms = new MemoryStream(zipBytes, writable: false);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            ZipArchiveEntry? best = null;
            foreach (var entry in zip.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name) || entry.FullName.EndsWith("/", StringComparison.Ordinal))
                    continue;

                if (!entry.Name.Equals("SKILL.md", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (best == null || entry.FullName.Length < best.FullName.Length)
                    best = entry;
            }

            if (best == null)
                return null;

            using var stream = best.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }
}
