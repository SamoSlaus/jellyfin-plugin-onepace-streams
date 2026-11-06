using Jellyfin.Plugin.OnePaceStreams.Catalog;
using Jellyfin.Plugin.OnePaceStreams.Configuration;
using Jellyfin.Plugin.OnePaceStreams.Sync;

namespace Jellyfin.Plugin.OnePaceStreams.Tests;

public class LibraryPlannerTests
{
    private static readonly PluginConfiguration Config = new() { Language = Language.Sub, Quality = 1080, DownloadArtwork = true };

    [Fact]
    public void WritesSeasonsEpisodesAndSpecials()
    {
        var plan = LibraryPlanner.Build(CatalogParserTests.LoadSample(), Config);
        var paths = plan.Files.Select(f => f.RelativePath).ToList();

        Assert.Equal(
            [
                P("One Pace", "tvshow.nfo"),
                P("One Pace", "Season 01", "One Pace - S01E01.strm"),
                P("One Pace", "Season 01", "One Pace - S01E01.nfo"),
                P("One Pace", "Season 01", "One Pace - S01E02.strm"),
                P("One Pace", "Season 01", "One Pace - S01E02.nfo"),
                P("One Pace", "Season 01", "season.nfo"),
                P("One Pace", "Season 02", "One Pace - S02E01.strm"),
                P("One Pace", "Season 02", "One Pace - S02E01.nfo"),
                P("One Pace", "Season 02", "season.nfo"),
                P("One Pace", "Season 00", "One Pace - S00E01.strm"),
                P("One Pace", "Season 00", "One Pace - S00E01.nfo"),
                P("One Pace", "Season 00", "season.nfo"),
            ],
            paths);
        Assert.Equal(4, plan.EpisodeCount);
    }

    [Fact]
    public void StrmContainsDirectPixeldrainUrl()
    {
        var plan = LibraryPlanner.Build(CatalogParserTests.LoadSample(), Config);

        Assert.Equal("https://pixeldrain.net/api/file/Sub1080a\n", Content(plan, "One Pace - S01E01.strm"));
        Assert.Equal("https://pixeldrain.net/api/file/Sub720b\n", Content(plan, "One Pace - S01E02.strm"));
        Assert.Equal("https://pixeldrain.net/api/file/DubAlt480\n", Content(plan, "One Pace - S02E01.strm"));
    }

    [Fact]
    public void EpisodeNfoIsEscapedAndNumbered()
    {
        var nfo = Content(LibraryPlanner.Build(CatalogParserTests.LoadSample(), Config), "One Pace - S01E02.nfo");

        Assert.Contains("<title>Episode \"Two\"</title>", nfo, StringComparison.Ordinal);
        Assert.Contains("<season>1</season>", nfo, StringComparison.Ordinal);
        Assert.Contains("<episode>2</episode>", nfo, StringComparison.Ordinal);
        Assert.Contains("Manga chapters: 2–7", nfo, StringComparison.Ordinal);
    }

    [Fact]
    public void SeasonNfoHasArcTitle()
    {
        var plan = LibraryPlanner.Build(CatalogParserTests.LoadSample(), Config);
        var nfo = plan.Files.Single(f => f.RelativePath == P("One Pace", "Season 01", "season.nfo")).Content;

        Assert.Contains("<title>First Arc</title>", nfo, StringComparison.Ordinal);
        Assert.Contains("<seasonnumber>1</seasonnumber>", nfo, StringComparison.Ordinal);
    }

    [Fact]
    public void PlansArtworkOnlyWhenEnabled()
    {
        var catalog = CatalogParserTests.LoadSampleCatalog();

        Assert.Equal(
            [
                P("One Pace", "backdrop.jpg"),
                P("One Pace", "logo.png"),
                P("One Pace", "Season 01", "One Pace - S01E01-thumb.jpg"),
                P("One Pace", "Season 01", "One Pace - S01E02-thumb.jpg"),
                P("One Pace", "Season 01", "folder.jpg"),
            ],
            LibraryPlanner.Build(catalog.Arcs, Config, catalog.LogoUrl).Images.Select(i => i.RelativePath));
        Assert.Equal(
            "https://onepace.net/_next/image?url=%2F_next%2Fstatic%2Fmedia%2Ffirst.jpg&w=1920&q=75",
            LibraryPlanner.Build(catalog.Arcs, Config, catalog.LogoUrl).Images[0].Source.AbsoluteUri);
        Assert.Empty(LibraryPlanner.Build(catalog.Arcs, new PluginConfiguration { DownloadArtwork = false }, catalog.LogoUrl).Images);
    }

    [Fact]
    public void SkipsEpisodesWithoutAcceptableVariant()
    {
        var config = new PluginConfiguration { Language = Language.Sub, AllowLanguageFallback = false };
        var plan = LibraryPlanner.Build(CatalogParserTests.LoadSample(), config);

        Assert.Equal(1, plan.SkippedEpisodes);
        Assert.DoesNotContain(plan.Files, f => f.RelativePath.Contains("Season 02", StringComparison.Ordinal));
    }

    [Fact]
    public void ExcludesSpecialsWhenDisabled()
    {
        var plan = LibraryPlanner.Build(CatalogParserTests.LoadSample(), new PluginConfiguration { IncludeSpecials = false });

        Assert.DoesNotContain(plan.Files, f => f.RelativePath.Contains("Season 00", StringComparison.Ordinal));
    }

    private static string P(params string[] parts) => Path.Combine(parts);

    private static string Content(LibraryPlan plan, string fileName) =>
        plan.Files.Single(f => Path.GetFileName(f.RelativePath) == fileName).Content;
}
