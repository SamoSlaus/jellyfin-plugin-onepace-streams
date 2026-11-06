using Jellyfin.Plugin.OnePaceStreams.Catalog;
using Jellyfin.Plugin.OnePaceStreams.Configuration;

namespace Jellyfin.Plugin.OnePaceStreams.Sync;

/// <summary>Picks the best file for an episode according to the settings.</summary>
public static class VariantSelector
{
    /// <summary>Selects a source: language first, then cut, then the closest quality not above the preferred one.</summary>
    /// <param name="sources">Available sources.</param>
    /// <param name="config">Settings.</param>
    /// <returns>The chosen source, or null if none is acceptable.</returns>
    public static VideoSource? Select(IReadOnlyList<VideoSource> sources, PluginConfiguration config)
    {
        foreach (var language in LanguageOrder(config.Language, config.AllowLanguageFallback))
        {
            var forLanguage = sources.Where(s => s.Language == language).ToList();
            foreach (var cut in CutOrder(config.PreferExtendedCut))
            {
                var forCut = forLanguage.Where(s => s.Cut == cut).ToList();
                if (forCut.Count > 0)
                {
                    return forCut.Where(s => s.Quality <= config.Quality).MaxBy(s => s.Quality)
                        ?? forCut.MinBy(s => s.Quality);
                }
            }
        }

        return null;
    }

    private static Language[] LanguageOrder(Language preferred, bool fallback)
    {
        if (!fallback)
        {
            return [preferred];
        }

        return preferred switch
        {
            Language.Dub => [Language.Dub, Language.DubCC, Language.Sub],
            Language.DubCC => [Language.DubCC, Language.Dub, Language.Sub],
            _ => [Language.Sub, Language.Dub, Language.DubCC],
        };
    }

    private static Cut[] CutOrder(bool preferExtended) =>
        preferExtended ? [Cut.Extended, Cut.Normal, Cut.Alternate] : [Cut.Normal, Cut.Extended, Cut.Alternate];
}
