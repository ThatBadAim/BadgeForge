using System.Text;
using System.Text.RegularExpressions;

namespace BadgeForge.Core.Templates.Tokens;

/// <summary>
/// The single definition of how dynamic data tokens are written inside template strings.
/// The canonical form is <c>{{FullName}}</c>; the legacy single-brace form <c>{FullName}</c> saved by earlier
/// templates is still recognised. Every layer, renderer and validator parses tokens through this class, so the
/// two syntaxes can never drift apart.
/// </summary>
public static class TokenSyntax
{
    /// <summary>
    /// Longest value substituted for a token. Anything longer is cut, so a pasted paragraph can't stall layout.
    /// </summary>
    public const int MaxValueLength = 512;

    // Double braces are tried first so "{{Name}}" is one token rather than "{" + "{Name}" + "}"
    private static readonly Regex TokenRegex = new(
        @"\{\{\s*(?<token>[^{}]+?)\s*\}\}|\{(?<token>[^{}]+)\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Writes a token in the canonical syntax: "FullName" becomes "{{FullName}}".
    /// </summary>
    public static string Format(string tokenName) => "{{" + NormalizeName(tokenName) + "}}";

    /// <summary>
    /// Strips braces and whitespace from a token reference: "{{ FullName }}", "{FullName}" and "FullName" all
    /// become "FullName".
    /// </summary>
    public static string NormalizeName(string? token) =>
        string.IsNullOrWhiteSpace(token) ? string.Empty : token.Trim().Trim('{', '}').Trim();

    /// <summary>
    /// True when the text contains at least one token.
    /// </summary>
    public static bool ContainsTokens(string? text) => !string.IsNullOrEmpty(text) && TokenRegex.IsMatch(text);

    /// <summary>
    /// Token names referenced by the text, in order of appearance (duplicates included).
    /// </summary>
    public static IEnumerable<string> Extract(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        foreach (Match match in TokenRegex.Matches(text))
        {
            string name = match.Groups["token"].Value.Trim();
            if (name.Length > 0)
            {
                yield return name;
            }
        }
    }

    /// <summary>
    /// Replaces every token with the value <paramref name="resolve"/> returns for its name. A null result keeps the
    /// raw token text, so an unmapped field stays visible instead of silently vanishing.
    /// Substitution is a single pass: braces inside substituted values are never parsed as tokens.
    /// </summary>
    public static string Replace(string? text, Func<string, string?> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return TokenRegex.Replace(text, match => resolve(match.Groups["token"].Value.Trim()) ?? match.Value);
    }

    /// <summary>
    /// Replaces tokens with values from a field dictionary. Values are looked up by bare name first, then by the
    /// literal token text (records built by older code store "{Photo}"-style keys).
    /// </summary>
    public static string Replace(string? text, IReadOnlyDictionary<string, string> fieldData, bool sanitize = false)
    {
        ArgumentNullException.ThrowIfNull(fieldData);
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return TokenRegex.Replace(text, match =>
        {
            string name = match.Groups["token"].Value.Trim();
            if (!fieldData.TryGetValue(name, out var value) && !fieldData.TryGetValue(match.Value, out value))
            {
                return match.Value;
            }

            return sanitize ? SanitizeValue(value) : value ?? string.Empty;
        });
    }

    /// <summary>
    /// Makes a record value safe to print: control characters (tabs, stray line breaks, NULs from bad exports) become
    /// spaces, runs of whitespace collapse, the ends are trimmed, and the length is capped at
    /// <see cref="MaxValueLength"/> characters without splitting a surrogate pair.
    /// </summary>
    public static string SanitizeValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(Math.Min(value.Length, MaxValueLength));
        bool pendingSpace = false;
        foreach (char c in value)
        {
            if (char.IsControl(c) || char.IsWhiteSpace(c) || (int)c is 0x2028 or 0x2029 or 0xFEFF)
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(c);
            if (builder.Length >= MaxValueLength)
            {
                break;
            }
        }

        if (builder.Length > MaxValueLength)
        {
            builder.Length = MaxValueLength;
        }

        if (builder.Length > 0 && char.IsHighSurrogate(builder[^1]))
        {
            builder.Length--;
        }

        return builder.ToString();
    }
}
