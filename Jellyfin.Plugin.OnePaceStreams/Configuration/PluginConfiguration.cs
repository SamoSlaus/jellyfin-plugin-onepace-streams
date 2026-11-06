using Jellyfin.Plugin.OnePaceStreams.Catalog;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.OnePaceStreams.Configuration;

/// <summary>Plugin settings.</summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Gets or sets the absolute folder to write into; empty means "&lt;Jellyfin data&gt;/onepace".</summary>
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the preferred language.</summary>
    public Language Language { get; set; } = Language.Sub;

    /// <summary>Gets or sets the preferred vertical resolution (480, 720 or 1080).</summary>
    public int Quality { get; set; } = 1080;

    /// <summary>Gets or sets a value indicating whether Extended Cut episodes are preferred.</summary>
    public bool PreferExtendedCut { get; set; }

    /// <summary>Gets or sets a value indicating whether other languages may be used when the preferred one is missing.</summary>
    public bool AllowLanguageFallback { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether specials are written to Season 00.</summary>
    public bool IncludeSpecials { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether arc and episode images are downloaded.</summary>
    public bool DownloadArtwork { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the next sync creates a Shows library for the output folder (if none exists). Reset after use.</summary>
    public bool CreateLibrary { get; set; } = true;
}
