using Jellyfin.Plugin.OnePaceStreams.Catalog;
using Jellyfin.Plugin.OnePaceStreams.Configuration;
using Jellyfin.Plugin.OnePaceStreams.Sync;

namespace Jellyfin.Plugin.OnePaceStreams.Tests;

public class VariantSelectorTests
{
    private static readonly VideoSource[] Sources =
    [
        new(Language.Sub, Cut.Normal, 480, "s480"),
        new(Language.Sub, Cut.Normal, 720, "s720"),
        new(Language.Sub, Cut.Extended, 720, "sx720"),
        new(Language.Dub, Cut.Normal, 1080, "d1080"),
    ];

    [Theory]
    [InlineData(1080, "s720")]
    [InlineData(720, "s720")]
    [InlineData(480, "s480")]
    [InlineData(360, "s480")]
    public void PicksClosestQualityNotAbovePreferred(int quality, string expected)
    {
        var config = new PluginConfiguration { Language = Language.Sub, Quality = quality };

        Assert.Equal(expected, VariantSelector.Select(Sources, config)?.FileId);
    }

    [Fact]
    public void PrefersExtendedCutWhenRequested()
    {
        var config = new PluginConfiguration { Language = Language.Sub, Quality = 1080, PreferExtendedCut = true };

        Assert.Equal("sx720", VariantSelector.Select(Sources, config)?.FileId);
    }

    [Fact]
    public void UsesExtendedCutWhenNormalIsMissing()
    {
        VideoSource[] sources = [new(Language.Sub, Cut.Alternate, 720, "alt"), new(Language.Sub, Cut.Extended, 480, "ext")];

        Assert.Equal("ext", VariantSelector.Select(sources, new PluginConfiguration { Language = Language.Sub })?.FileId);
    }

    [Fact]
    public void FallsBackToOtherLanguage()
    {
        var config = new PluginConfiguration { Language = Language.DubCC, AllowLanguageFallback = true };

        Assert.Equal("d1080", VariantSelector.Select(Sources, config)?.FileId);
    }

    [Fact]
    public void ReturnsNullWithoutFallback()
    {
        var config = new PluginConfiguration { Language = Language.DubCC, AllowLanguageFallback = false };

        Assert.Null(VariantSelector.Select(Sources, config));
    }
}
