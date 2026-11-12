using System.Text;

namespace Laraue.Apps.Identity.Services;

/// <summary>
/// Where a user came to a service from (BRD-295). Callers pass what they saw, raw (<see cref="SignUpAttribution"/>), and
/// this is the one place that turns it into a source: the caller's explicit source (a bot <c>/start</c> parameter, a
/// Mini App <c>start_param</c>, a caller-specific rule such as Boards' invite links), else the landing link's
/// <c>ref</c>, else its <c>utm_source</c>, else the referring site - a known one by its short name, a search engine by
/// its name (both from <see cref="SignUpSourceOptions"/>), any other by its host. Then normalized, so whatever reaches
/// the database is short and plain.
/// </summary>
public static class SignUpSource
{
    public const int MaxLength = 64;

    /// <summary>
    /// The source and the referring site's host for what a caller saw. Both null when nothing tells where the user
    /// came from.
    /// </summary>
    public static ResolvedSignUpSource Resolve(SignUpAttribution attribution, SignUpSourceOptions options)
    {
        var landing = TryParseUrl(attribution.LandingUrl);
        var referrerHost = TryParseUrl(attribution.Referrer)?.Host.ToLowerInvariant();

        // A link from the same site (the user moved between its own pages before landing) isn't where they came from.
        if (referrerHost is not null && landing is not null && referrerHost == landing.Host.ToLowerInvariant())
        {
            referrerHost = null;
        }

        var fromLink = landing is null
            ? null
            : FirstNonEmpty(QueryValue(landing, "ref"), QueryValue(landing, "utm_source"));

        var source = FirstNonEmpty(attribution.Source, fromLink)
            ?? (referrerHost is null ? null : SourceOfHost(referrerHost, options));

        return new ResolvedSignUpSource(Normalize(source), referrerHost);
    }

    private static string SourceOfHost(string host, SignUpSourceOptions options)
    {
        foreach (var referrer in options.Referrers)
        {
            var domain = referrer.Domain.Trim().ToLowerInvariant();
            if (domain.Length > 0 && (host == domain || host.EndsWith("." + domain, StringComparison.Ordinal)))
            {
                return referrer.Source;
            }
        }

        // google.com, www.google.de, yandex.ru: any label but the last (the country/top-level part).
        var labels = host.Split('.');
        foreach (var engine in options.SearchEngines)
        {
            var name = engine.Trim().ToLowerInvariant();
            if (labels.Length >= 2 && labels[..^1].Contains(name))
            {
                return name;
            }
        }

        return host;
    }

    private static Uri? TryParseUrl(string? url)
    {
        return Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? uri
            : null;
    }

    private static string? QueryValue(Uri url, string name)
    {
        foreach (var pair in url.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var key = separator < 0 ? pair : pair[..separator];
            if (string.Equals(Uri.UnescapeDataString(key), name, StringComparison.OrdinalIgnoreCase))
            {
                return separator < 0 ? null : Uri.UnescapeDataString(pair[(separator + 1)..].Replace('+', ' '));
            }
        }

        return null;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
    }

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

/// <summary>
/// What a caller saw about where a user came from, raw: its own explicit source (if any), the first page the user opened
/// on its site, and the site that sent them there.
/// </summary>
public sealed record SignUpAttribution(string? Source, string? LandingUrl, string? Referrer)
{
    public static readonly SignUpAttribution None = new(null, null, null);
}

/// <param name="Source">Normalized; null when unknown.</param>
/// <param name="ReferrerHost">The referring site's host (not its URL, which can carry personal details); null when
/// there was none or it was the same site.</param>
public sealed record ResolvedSignUpSource(string? Source, string? ReferrerHost);

/// <summary>
/// The referring sites and search engines <see cref="SignUpSource.Resolve"/> knows (section <c>SignUpSources</c> of
/// InternalApiHost's <c>appsettings.json</c>), so a new launch site needs no code change.
/// </summary>
public sealed class SignUpSourceOptions
{
    /// <summary>
    /// Sites with a short name of their own: a referrer on <see cref="SignUpReferrer.Domain"/> or any of its
    /// subdomains becomes <see cref="SignUpReferrer.Source"/>.
    /// </summary>
    public SignUpReferrer[] Referrers { get; set; } = [];

    /// <summary>
    /// Search engines by name, matched on any of their country domains (google.de, www.google.com, yandex.ru); the
    /// source is the name.
    /// </summary>
    public string[] SearchEngines { get; set; } = [];
}

public sealed class SignUpReferrer
{
    public string Domain { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;
}
