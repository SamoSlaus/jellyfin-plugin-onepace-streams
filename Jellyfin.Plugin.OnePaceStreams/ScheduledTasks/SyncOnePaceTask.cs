using Jellyfin.Plugin.OnePaceStreams.Catalog;
using Jellyfin.Plugin.OnePaceStreams.Sync;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.OnePaceStreams.ScheduledTasks;

/// <summary>Scheduled task that syncs the One Pace .strm library.</summary>
public sealed class SyncOnePaceTask(
    OnePaceCatalogClient catalogClient,
    StrmSyncService syncService,
    ILibraryManager libraryManager,
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
        var config = Plugin.Instance?.Configuration ?? throw new InvalidOperationException("Plugin is not initialised.");

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

        if (result.Changed)
        {
            await libraryManager.ValidateMediaLibrary(new Progress<double>(p => progress.Report(60 + (p * 0.4))), cancellationToken).ConfigureAwait(false);
        }

        progress.Report(100);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() =>
    [
        new TaskTriggerInfo { Type = TaskTriggerInfoType.DailyTrigger, TimeOfDayTicks = TimeSpan.FromHours(4).Ticks },
    ];
}
