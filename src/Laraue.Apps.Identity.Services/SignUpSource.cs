using System.Text;

namespace Laraue.Apps.Identity.Services;

/// <summary>
/// Where a user came to a service from (BRD-295): a <c>ref</c>/<c>utm_source</c> value from a landing link, a bot's
/// <c>/start</c> parameter, a Mini App <c>start_param</c>, as the caller found it. Callers normalize it too; this is the
/// last line, so whatever reaches the database is short and plain.
/// </summary>
public static class SignUpSource
{
    public const int MaxLength = 64;

    /// <summary>
    /// Lower-case, only <c>a-z</c>, <c>0-9</c>, <c>.</c>, <c>-</c> and <c>_</c> (anything else dropped), cut to
    /// <see cref="MaxLength"/>. Null when nothing is left.
    /// </summary>
    public static string? Normalize(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        var builder = new StringBuilder(Math.Min(source.Length, MaxLength));
        foreach (var c in source.Trim().ToLowerInvariant())
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '-' or '_')
            {
                builder.Append(c);
                if (builder.Length == MaxLength)
                {
                    break;
                }
            }
        }

        return builder.Length == 0 ? null : builder.ToString();
    }
}
