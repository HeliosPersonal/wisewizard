using System.Security.Cryptography;
using System.Text;

namespace WiseWizard.Core.Services;

/// <summary>
/// Computes a stable content hash used to deduplicate Raw documents within a Run
/// (data-model.md — <c>ux_raw_documents_run_hash</c>; PRD §5 AC-04). The hash is a
/// SHA-256 hex digest of the normalized salient fields (title + content, falling back
/// to the URL) so the same article/filing collected twice yields the same hash.
/// </summary>
public static class ContentHasher
{
    /// <summary>
    /// Computes the dedup hash from the salient parts of a document. Inputs are normalized
    /// (trimmed, collapsed whitespace, lowercased) before hashing so trivial formatting
    /// differences do not defeat dedup. The result is a lowercase hex SHA-256 digest.
    /// </summary>
    public static string Compute(string? title, string? content, string? url = null)
    {
        var canonical = string.Join(
            "",
            Normalize(title),
            Normalize(content),
            Normalize(url));

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexStringLower(bytes);
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var collapsed = new StringBuilder(value.Length);
        var previousWasWhitespace = false;
        foreach (var c in value.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                if (!previousWasWhitespace)
                {
                    collapsed.Append(' ');
                    previousWasWhitespace = true;
                }
            }
            else
            {
                collapsed.Append(char.ToLowerInvariant(c));
                previousWasWhitespace = false;
            }
        }

        return collapsed.ToString();
    }
}
