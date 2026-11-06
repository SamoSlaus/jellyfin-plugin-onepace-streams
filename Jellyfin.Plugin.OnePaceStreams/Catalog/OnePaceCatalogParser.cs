using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Jellyfin.Plugin.OnePaceStreams.Catalog;

/// <summary>Parses the https://onepace.net/en/watch page.</summary>
public static partial class OnePaceCatalogParser
{
    private const string MangaChaptersLabel = "Covered manga chapters";
    private const string SpecialPrefix = "Special:";

    /// <summary>Parses all arcs, in page order, and the One Pace logo.</summary>
    /// <param name="html">Page HTML.</param>
    /// <param name="baseUri">URL the page was loaded from.</param>
    /// <returns>The catalog.</returns>
    public static OnePaceCatalog Parse(string html, Uri baseUri)
    {
        var document = new HtmlParser().ParseDocument(html);
        var arcs = new List<Arc>();

        foreach (var heading in document.QuerySelectorAll("h2[id$='-title']"))
        {
            var segment = heading.Closest("li[data-timeline-segment]");
            if (segment is null)
            {
                continue;
            }

            var title = Normalize(heading.TextContent);
            var isSpecial = title.StartsWith(SpecialPrefix, StringComparison.OrdinalIgnoreCase);
            if (isSpecial)
            {
                title = title[SpecialPrefix.Length..].Trim();
            }

            var header = heading.ParentElement;
            arcs.Add(new Arc(
                segment.GetAttribute("data-timeline-segment") ?? string.Empty,
                title,
                isSpecial,
                GetLabeledValue(header, MangaChaptersLabel),
                GetParagraph(header),
                GetImageUrl(segment, baseUri),
                ParseEpisodes(segment, baseUri)));
        }

        return new OnePaceCatalog(arcs, ParseLogoUrl(document, baseUri));
    }

    private static Uri? ParseLogoUrl(IDocument document, Uri baseUri)
    {
        foreach (var meta in document.QuerySelectorAll("meta[property='og:image'][content]"))
        {
            if (Uri.TryCreate(baseUri, meta.GetAttribute("content"), out var uri)
                && IsCatalogHost(uri, baseUri)
                && uri.AbsolutePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                return uri;
            }
        }

        return null;
    }

    internal static bool TryParseVariant(string label, out Language language, out Cut cut)
    {
        var parts = Normalize(label).Split('·', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        language = default;
        cut = Cut.Normal;
        if (parts.Length == 0)
        {
            return false;
        }

        switch (parts[0].ToUpperInvariant())
        {
            case "ENGLISH SUBTITLES":
                language = Language.Sub;
                break;
            case "ENGLISH DUB":
                language = Language.Dub;
                break;
            case var other when other.StartsWith("ENGLISH DUB WITH ", StringComparison.Ordinal):
                language = Language.DubCC;
                break;
            default:
                return false;
        }

        if (parts.Length > 1)
        {
            cut = parts.Skip(1).Any(p => p.Contains("Extended", StringComparison.OrdinalIgnoreCase)) ? Cut.Extended : Cut.Alternate;
        }

        return true;
    }

    private static List<Episode> ParseEpisodes(IElement segment, Uri baseUri)
    {
        var episodes = new List<Episode>();
        foreach (var item in segment.QuerySelectorAll("details ol > li[value]"))
        {
            if (!int.TryParse(item.GetAttribute("value"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                continue;
            }

            var heading = item.QuerySelector("h3");
            var title = heading is null ? string.Empty : EpisodeNumberPrefix().Replace(Normalize(heading.TextContent), string.Empty);
            var header = heading?.ParentElement;

            episodes.Add(new Episode(
                number,
                title.Length == 0 ? $"Episode {number}" : title,
                GetParagraph(header),
                GetLabeledValue(header, MangaChaptersLabel),
                GetImageUrl(item, baseUri),
                ParseSources(item)));
        }

        return episodes;
    }

    private static List<VideoSource> ParseSources(IElement episode)
    {
        var sources = new List<VideoSource>();
        foreach (var option in episode.QuerySelectorAll("ul[aria-label='Watch and download options'] > li"))
        {
            var label = option.Children.FirstOrDefault(c => c.LocalName == "span");
            if (label is null || !TryParseVariant(label.TextContent, out var language, out var cut))
            {
                continue;
            }

            foreach (var link in option.QuerySelectorAll("a[href]"))
            {
                var file = PixeldrainFileLink().Match(link.GetAttribute("href") ?? string.Empty);
                var quality = QualityText().Match(link.TextContent);
                if (file.Success && quality.Success)
                {
                    sources.Add(new VideoSource(
                        language,
                        cut,
                        int.Parse(quality.Groups[1].Value, CultureInfo.InvariantCulture),
                        file.Groups[1].Value));
                }
            }
        }

        return sources;
    }

    private static string? GetLabeledValue(IElement? scope, string label)
    {
        if (scope is null)
        {
            return null;
        }

        foreach (var span in scope.QuerySelectorAll("span"))
        {
            var first = span.Children.FirstOrDefault(c => c.LocalName == "span");
            if (first is not null && string.Equals(Normalize(first.TextContent), label, StringComparison.OrdinalIgnoreCase))
            {
                var value = Normalize(span.TextContent)[label.Length..].Trim();
                return value.Length == 0 ? null : value;
            }
        }

        return null;
    }

    private static string? GetParagraph(IElement? scope)
    {
        var paragraph = scope?.Children.FirstOrDefault(c => c.LocalName == "p");
        return paragraph is null ? null : Normalize(paragraph.TextContent);
    }

    private static Uri? GetImageUrl(IElement scope, Uri baseUri)
    {
        var src = scope.QuerySelector("img[src]")?.GetAttribute("src");
        if (string.IsNullOrEmpty(src) || !Uri.TryCreate(baseUri, src, out var uri) || !IsCatalogHost(uri, baseUri))
        {
            return null;
        }

        // Next.js rejects widths not in its configured list (640, 750, 828, 1080, 1200, 1920, 2048, 3840).
        return new Uri(ImageWidthParam().Replace(uri.AbsoluteUri, "${1}1920"));
    }

    // Only fetch artwork from the catalog's own host.
    private static bool IsCatalogHost(Uri uri, Uri baseUri) =>
        uri.Scheme == Uri.UriSchemeHttps && string.Equals(uri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string text) => Whitespace().Replace(text.Replace('\u00a0', ' '), " ").Trim();

    [GeneratedRegex(@"^https?://pixeldrain\.(?:net|com)/u/([A-Za-z0-9]+)/?$")]
    private static partial Regex PixeldrainFileLink();

    [GeneratedRegex(@"(\d{3,4})p\b")]
    private static partial Regex QualityText();

    [GeneratedRegex(@"^\d+\s*:\s*")]
    private static partial Regex EpisodeNumberPrefix();

    [GeneratedRegex(@"([?&]w=)\d+")]
    private static partial Regex ImageWidthParam();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
