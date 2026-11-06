using System.Globalization;
using System.Xml.Linq;

namespace Jellyfin.Plugin.OnePaceStreams.Sync;

/// <summary>Builds Kodi/Jellyfin NFO documents.</summary>
public static class NfoWriter
{
    /// <summary>Builds tvshow.nfo.</summary>
    /// <returns>The XML.</returns>
    public static string Series() => Serialize(new XElement(
        "tvshow",
        new XElement("title", "One Pace"),
        new XElement("originaltitle", "One Pace"),
        new XElement("plot", "One Pace is a fan project that recuts the One Piece anime to match the pacing of the manga."),
        new XElement("genre", "Anime")));

    /// <summary>Builds season.nfo.</summary>
    /// <param name="number">Season number.</param>
    /// <param name="title">Arc title.</param>
    /// <param name="plot">Arc description.</param>
    /// <param name="mangaChapters">Covered manga chapters.</param>
    /// <returns>The XML.</returns>
    public static string Season(int number, string title, string? plot, string? mangaChapters) => Serialize(new XElement(
        "season",
        new XElement("title", title),
        new XElement("seasonnumber", number.ToString(CultureInfo.InvariantCulture)),
        new XElement("plot", Plot(plot, mangaChapters))));

    /// <summary>Builds an episode NFO.</summary>
    /// <param name="title">Episode title.</param>
    /// <param name="season">Season number.</param>
    /// <param name="episode">Episode number.</param>
    /// <param name="plot">Episode description.</param>
    /// <param name="mangaChapters">Covered manga chapters.</param>
    /// <returns>The XML.</returns>
    public static string Episode(string title, int season, int episode, string? plot, string? mangaChapters) => Serialize(new XElement(
        "episodedetails",
        new XElement("title", title),
        new XElement("showtitle", "One Pace"),
        new XElement("season", season.ToString(CultureInfo.InvariantCulture)),
        new XElement("episode", episode.ToString(CultureInfo.InvariantCulture)),
        new XElement("plot", Plot(plot, mangaChapters))));

    private static string Plot(string? plot, string? mangaChapters)
    {
        var chapters = string.IsNullOrEmpty(mangaChapters) ? null : $"Manga chapters: {mangaChapters}";
        return string.Join("\n\n", new[] { plot, chapters }.Where(s => !string.IsNullOrEmpty(s)));
    }

    private static string Serialize(XElement root) =>
        "<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>\n" + root + "\n";
}
