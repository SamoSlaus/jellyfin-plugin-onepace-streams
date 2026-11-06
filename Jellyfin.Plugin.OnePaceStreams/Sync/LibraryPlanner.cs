using System.Globalization;
using Jellyfin.Plugin.OnePaceStreams.Catalog;
using Jellyfin.Plugin.OnePaceStreams.Configuration;

namespace Jellyfin.Plugin.OnePaceStreams.Sync;

/// <summary>A text file to write, relative to the output folder.</summary>
public sealed record PlannedFile(string RelativePath, string Content);

/// <summary>An image to download, relative to the output folder.</summary>
public sealed record PlannedImage(string RelativePath, Uri Source);

/// <summary>The full desired library layout.</summary>
public sealed record LibraryPlan(IReadOnlyList<PlannedFile> Files, IReadOnlyList<PlannedImage> Images, int EpisodeCount, int SkippedEpisodes);

/// <summary>Turns the catalog into the folder/file layout.</summary>
public static class LibraryPlanner
{
    /// <summary>Series folder name inside the output folder.</summary>
    public const string SeriesFolder = "One Pace";

    /// <summary>Builds the plan.</summary>
    /// <param name="arcs">Arcs in page order.</param>
    /// <param name="config">Settings.</param>
    /// <returns>The plan.</returns>
    public static LibraryPlan Build(IReadOnlyList<Arc> arcs, PluginConfiguration config)
    {
        var files = new List<PlannedFile> { new(Path.Combine(SeriesFolder, "tvshow.nfo"), NfoWriter.Series()) };
        var images = new List<PlannedImage>();
        var season = 0;
        var specialEpisode = 0;
        var episodeCount = 0;
        var skipped = 0;
        var specialsSeasonWritten = false;

        foreach (var arc in arcs)
        {
            // Specials are numbered even when excluded so toggling the option doesn't renumber them.
            var seasonNumber = arc.IsSpecial ? 0 : ++season;
            if (arc.IsSpecial && !config.IncludeSpecials)
            {
                specialEpisode += arc.Episodes.Count;
                continue;
            }

            var seasonDir = Path.Combine(SeriesFolder, SeasonFolder(seasonNumber));
            var written = 0;

            foreach (var episode in arc.Episodes)
            {
                var episodeNumber = arc.IsSpecial ? ++specialEpisode : episode.Number;
                var source = VariantSelector.Select(episode.Sources, config);
                if (source is null)
                {
                    skipped++;
                    continue;
                }

                var baseName = EpisodeBaseName(seasonNumber, episodeNumber);
                files.Add(new(Path.Combine(seasonDir, baseName + ".strm"), StreamUrl(source.FileId) + "\n"));
                files.Add(new(
                    Path.Combine(seasonDir, baseName + ".nfo"),
                    NfoWriter.Episode(episode.Title, seasonNumber, episodeNumber, episode.Description, episode.MangaChapters)));
                if (config.DownloadArtwork && episode.ImageUrl is not null)
                {
                    images.Add(new(Path.Combine(seasonDir, baseName + "-thumb.jpg"), episode.ImageUrl));
                }

                written++;
                episodeCount++;
            }

            if (written == 0)
            {
                continue;
            }

            if (!arc.IsSpecial)
            {
                files.Add(new(Path.Combine(seasonDir, "season.nfo"), NfoWriter.Season(seasonNumber, arc.Title, arc.Description, arc.MangaChapters)));
                if (config.DownloadArtwork && arc.ImageUrl is not null)
                {
                    images.Add(new(Path.Combine(seasonDir, "folder.jpg"), arc.ImageUrl));
                }
            }
            else if (!specialsSeasonWritten)
            {
                files.Add(new(Path.Combine(seasonDir, "season.nfo"), NfoWriter.Season(0, "Specials", null, null)));
                specialsSeasonWritten = true;
            }
        }

        return new LibraryPlan(files, images, episodeCount, skipped);
    }

    /// <summary>Gets the direct Pixeldrain download URL for a file.</summary>
    /// <param name="fileId">Pixeldrain file id.</param>
    /// <returns>The URL.</returns>
    public static string StreamUrl(string fileId) => "https://pixeldrain.net/api/file/" + fileId;

    /// <summary>Gets the season folder name.</summary>
    /// <param name="season">Season number.</param>
    /// <returns>Folder name.</returns>
    public static string SeasonFolder(int season) => string.Create(CultureInfo.InvariantCulture, $"Season {season:00}");

    /// <summary>Gets the stable file name (without extension) for an episode.</summary>
    /// <param name="season">Season number.</param>
    /// <param name="episode">Episode number.</param>
    /// <returns>File base name.</returns>
    public static string EpisodeBaseName(int season, int episode) =>
        string.Create(CultureInfo.InvariantCulture, $"{SeriesFolder} - S{season:00}E{episode:00}");
}
