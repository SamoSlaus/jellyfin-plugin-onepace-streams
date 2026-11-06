using Jellyfin.Plugin.OnePaceStreams.Catalog;
using Jellyfin.Plugin.OnePaceStreams.Configuration;
using Jellyfin.Plugin.OnePaceStreams.Sync;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.OnePaceStreams.ScheduledTasks;

/// <summary>Scheduled task that syncs the One Pace .strm library.</summary>
public sealed class SyncOnePaceTask(
    OnePaceCatalogClient catalogClient,
    StrmSyncService syncService,
    ILibraryManager libraryManager,
    IApplicationPaths applicationPaths,
    ILogger<SyncOnePaceTask> logger) : IScheduledTask
{
    /// <inheritdoc />
    public string Name => "Sync One Pace";

    /// <inheritdoc />
    public string Key => "OnePaceStreamsSync";

    /// <inheritdoc />
    public string Description => "Writes .strm and .nfo files for every One Pace episode, then scans the library.";

    /// <inheritdoc />
    public string Category => "One Pace";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var plugin = Plugin.Instance ?? throw new InvalidOperationException("Plugin is not initialised.");
        var config = plugin.Configuration;
        if (string.IsNullOrWhiteSpace(config.OutputPath))
        {
            config.OutputPath = Path.Combine(applicationPaths.DataPath, "onepace");
            plugin.SaveConfiguration();
        }

        progress.Report(0);
        var catalog = await catalogClient.GetCatalogAsync(cancellationToken).ConfigureAwait(false);
        progress.Report(20);

        var result = await syncService.SyncAsync(catalog, config, cancellationToken).ConfigureAwait(false);
        logger.LogInformation(
            "One Pace sync: {Episodes} episodes, {Written} written, {Unchanged} unchanged, {Deleted} deleted, {Skipped} without a matching variant",
            result.Episodes,
            result.Written,
            result.Unchanged,
            result.Deleted,
            result.SkippedEpisodes);
        progress.Report(60);

        var libraryCreated = false;
        if (config.CreateLibrary)
        {
            libraryCreated = await EnsureLibraryAsync(config.OutputPath).ConfigureAwait(false);
            config.CreateLibrary = false;
            plugin.SaveConfiguration();
        }

        if (result.Changed || libraryCreated)
        {
            await libraryManager.ValidateMediaLibrary(new Progress<double>(p => progress.Report(60 + (p * 0.4))), cancellationToken).ConfigureAwait(false);
        }

        progress.Report(100);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() =>
    [
        new TaskTriggerInfo { Type = TaskTriggerInfoType.StartupTrigger },
        new TaskTriggerInfo { Type = TaskTriggerInfoType.DailyTrigger, TimeOfDayTicks = TimeSpan.FromHours(4).Ticks },
    ];

    private async Task<bool> EnsureLibraryAsync(string outputPath)
    {
        var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputPath));
        var folders = libraryManager.GetVirtualFolders();
        var existing = folders.FirstOrDefault(f => f.Locations.Any(l => string.Equals(Path.TrimEndingDirectorySeparator(l), path, StringComparison.Ordinal)));
        if (existing is not null)
        {
            logger.LogInformation("Library {Name} already contains {Path}; not creating another one", existing.Name, path);
            return false;
        }

        var name = folders.Any(f => f.Name == "One Pace") ? "One Pace Streams" : "One Pace";
        await libraryManager.AddVirtualFolder(name, CollectionTypeOptions.tvshows, CreateLibraryOptions(path), refreshLibrary: false).ConfigureAwait(false);
        logger.LogInformation("Created library {Name} for {Path}", name, path);
        return true;
    }

    private static LibraryOptions CreateLibraryOptions(string path) => new()
    {
        PathInfos = [new MediaPathInfo { Path = path }],
        SeasonZeroDisplayName = "Specials",
        LocalMetadataReaderOrder = ["Nfo"],

        // The plugin owns the NFO files; Jellyfin must not rewrite them.
        MetadataSavers = [],
        SaveLocalMetadata = false,

        // These would download whole videos from Pixeldrain during scans.
        EnableChapterImageExtraction = false,
        ExtractChapterImagesDuringLibraryScan = false,
        EnableTrickplayImageExtraction = false,
        ExtractTrickplayImagesDuringLibraryScan = false,
        EnableLUFSScan = false,

        // Metadata and images come from the NFO and image files only.
        TypeOptions = [NoFetchers("Series"), NoFetchers("Season"), NoFetchers("Episode")],
    };

    private static TypeOptions NoFetchers(string type) => new()
    {
        Type = type,
        MetadataFetchers = [],
        MetadataFetcherOrder = [],
        ImageFetchers = [],
        ImageFetcherOrder = [],
    };
}
