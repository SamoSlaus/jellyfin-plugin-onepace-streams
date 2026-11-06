using Jellyfin.Plugin.OnePaceStreams.Catalog;

namespace Jellyfin.Plugin.OnePaceStreams.Tests;

public class CatalogParserTests
{
    private static readonly Uri BaseUri = new("https://onepace.net/en/watch");

    internal static IReadOnlyList<Arc> LoadSample() =>
        OnePaceCatalogParser.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "watch-sample.html")), BaseUri);

    [Fact]
    public void ParsesArcsInPageOrder()
    {
        var arcs = LoadSample();

        Assert.Equal(["first-arc", "second-arc", "a-special"], arcs.Select(a => a.Slug));
        Assert.Equal(["First Arc", "Second Arc", "A Special"], arcs.Select(a => a.Title));
        Assert.Equal([false, false, true], arcs.Select(a => a.IsSpecial));
    }

    [Fact]
    public void ParsesArcMetadata()
    {
        var arc = LoadSample()[0];

        Assert.Equal("1–7", arc.MangaChapters);
        Assert.Equal("Arc one description.", arc.Description);
        Assert.Equal(
            "https://onepace.net/_next/image?url=%2F_next%2Fstatic%2Fmedia%2Ffirst.jpg&w=1280&q=75",
            arc.ImageUrl?.AbsoluteUri);
    }

    [Fact]
    public void RejectsForeignImageHosts()
    {
        Assert.Null(LoadSample()[1].ImageUrl);
    }

    [Fact]
    public void ParsesEpisodes()
    {
        var episodes = LoadSample()[0].Episodes;

        Assert.Equal([1, 2], episodes.Select(e => e.Number));
        Assert.Equal(["Episode One Title", "Episode \"Two\""], episodes.Select(e => e.Title));
        Assert.Equal("Episode one description.", episodes[0].Description);
        Assert.Equal("2–7", episodes[1].MangaChapters);
    }

    [Fact]
    public void ParsesSourcesAndSkipsBatches()
    {
        var episodes = LoadSample()[0].Episodes;

        Assert.Equal(
            [
                new VideoSource(Language.Sub, Cut.Normal, 480, "Sub480a"),
                new VideoSource(Language.Sub, Cut.Normal, 720, "Sub720a"),
                new VideoSource(Language.Sub, Cut.Normal, 1080, "Sub1080a"),
                new VideoSource(Language.Dub, Cut.Normal, 720, "Dub720a"),
            ],
            episodes[0].Sources);
        Assert.Equal(
            [
                new VideoSource(Language.Sub, Cut.Normal, 720, "Sub720b"),
                new VideoSource(Language.Sub, Cut.Extended, 720, "SubExt720b"),
                new VideoSource(Language.DubCC, Cut.Normal, 720, "DubCC720b"),
            ],
            episodes[1].Sources);
    }

    [Theory]
    [InlineData("English Subtitles", Language.Sub, Cut.Normal)]
    [InlineData("English Dub", Language.Dub, Cut.Normal)]
    [InlineData("English Dub with Closed Captions", Language.DubCC, Cut.Normal)]
    [InlineData("English Dub\u00a0· Extended Cut", Language.Dub, Cut.Extended)]
    [InlineData("English Subtitles · G-8 Alternate Cut", Language.Sub, Cut.Alternate)]
    public void ParsesVariantLabels(string label, Language language, Cut cut)
    {
        Assert.True(OnePaceCatalogParser.TryParseVariant(label, out var actualLanguage, out var actualCut));
        Assert.Equal(language, actualLanguage);
        Assert.Equal(cut, actualCut);
    }

    [Theory]
    [InlineData("All-in-One · 1080p · 2.0 GiB")]
    [InlineData("German Subtitles")]
    [InlineData("")]
    public void RejectsUnknownLabels(string label)
    {
        Assert.False(OnePaceCatalogParser.TryParseVariant(label, out _, out _));
    }

    // Set ONEPACE_WATCH_HTML to a saved copy of https://onepace.net/en/watch to run this check against the real page.
    [Fact]
    public void ParsesRealPageWhenAvailable()
    {
        var path = Environment.GetEnvironmentVariable("ONEPACE_WATCH_HTML");
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Assert.Skip("ONEPACE_WATCH_HTML not set");
        }

        var arcs = OnePaceCatalogParser.Parse(File.ReadAllText(path), BaseUri);

        Assert.True(arcs.Count >= 30);
        var romanceDawn = arcs.Single(a => a.Slug == "romance-dawn");
        Assert.Equal(4, romanceDawn.Episodes.Count);
        Assert.Equal("1–7", romanceDawn.MangaChapters);
        Assert.NotNull(romanceDawn.Description);
        Assert.NotNull(romanceDawn.ImageUrl);
        Assert.All(romanceDawn.Episodes, e => Assert.NotNull(e.Description));
        Assert.True(arcs.Single(a => a.Slug == "wano").Episodes.Count >= 62);
        Assert.Contains(arcs, a => a.IsSpecial);
        Assert.All(arcs.SelectMany(a => a.Episodes), e => Assert.NotEmpty(e.Sources));
    }
}
