using Jellyfin.Plugin.OnePaceStreams.Catalog;
using Jellyfin.Plugin.OnePaceStreams.Configuration;
using Jellyfin.Plugin.OnePaceStreams.Sync;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.OnePaceStreams.Tests;

public sealed class StrmSyncServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "onepace-tests-" + Guid.NewGuid().ToString("N"));
    private readonly StrmSyncService _service = new(new NoHttpClientFactory(), NullLogger<StrmSyncService>.Instance);

    private PluginConfiguration Config => new() { OutputPath = _root, DownloadArtwork = false };

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task FirstRunWritesFilesAndSecondRunChangesNothing()
    {
        var arcs = CatalogParserTests.LoadSample();

        var first = await _service.SyncAsync(arcs, Config, CancellationToken.None);
        var stamp = File.GetLastWriteTimeUtc(Path.Combine(_root, "One Pace", "Season 01", "One Pace - S01E01.strm"));
        var second = await _service.SyncAsync(arcs, Config, CancellationToken.None);

        Assert.Equal(12, first.Written);
        Assert.True(File.Exists(Path.Combine(_root, StrmSyncService.ManifestFileName)));
        Assert.False(second.Changed);
        Assert.Equal(12, second.Unchanged);
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(Path.Combine(_root, "One Pace", "Season 01", "One Pace - S01E01.strm")));
    }

    [Fact]
    public async Task RewritesOnlyChangedFiles()
    {
        var arcs = CatalogParserTests.LoadSample();
        await _service.SyncAsync(arcs, Config, CancellationToken.None);

        var lowQuality = new PluginConfiguration { OutputPath = _root, DownloadArtwork = false, Quality = 480 };
        var result = await _service.SyncAsync(arcs, lowQuality, CancellationToken.None);

        Assert.Equal(1, result.Written);
        Assert.Equal(
            "https://pixeldrain.net/api/file/Sub480a\n",
            await File.ReadAllTextAsync(Path.Combine(_root, "One Pace", "Season 01", "One Pace - S01E01.strm")));
    }

    [Fact]
    public async Task RemovedArcDeletesOnlyItsOwnFiles()
    {
        var arcs = CatalogParserTests.LoadSample();
        await _service.SyncAsync(arcs, Config, CancellationToken.None);
        var userFile = Path.Combine(_root, "One Pace", "Season 02", "my-notes.txt");
        await File.WriteAllTextAsync(userFile, "keep me");
        var otherUserFile = Path.Combine(_root, "Other Show", "episode.mkv");
        Directory.CreateDirectory(Path.GetDirectoryName(otherUserFile)!);
        await File.WriteAllTextAsync(otherUserFile, "keep me too");

        var result = await _service.SyncAsync(arcs.Where(a => a.Slug != "second-arc").ToList(), Config, CancellationToken.None);

        Assert.Equal(3, result.Deleted);
        Assert.False(File.Exists(Path.Combine(_root, "One Pace", "Season 02", "One Pace - S02E01.strm")));
        Assert.True(File.Exists(userFile));
        Assert.True(File.Exists(otherUserFile));
        Assert.True(File.Exists(Path.Combine(_root, "One Pace", "Season 01", "One Pace - S01E01.strm")));
    }

    [Fact]
    public async Task RemovesEmptySeasonFolders()
    {
        var arcs = CatalogParserTests.LoadSample();
        await _service.SyncAsync(arcs, Config, CancellationToken.None);

        await _service.SyncAsync(arcs.Where(a => !a.IsSpecial).ToList(), Config, CancellationToken.None);

        Assert.False(Directory.Exists(Path.Combine(_root, "One Pace", "Season 00")));
    }

    [Fact]
    public async Task IgnoresManifestEntriesOutsideOutputFolder()
    {
        var outside = Path.Combine(Path.GetTempPath(), "onepace-outside-" + Guid.NewGuid().ToString("N") + ".txt");
        await File.WriteAllTextAsync(outside, "do not delete");
        try
        {
            Directory.CreateDirectory(_root);
            var relative = Path.GetRelativePath(_root, outside);
            await File.WriteAllTextAsync(
                Path.Combine(_root, StrmSyncService.ManifestFileName),
                System.Text.Json.JsonSerializer.Serialize(new[] { relative }));

            await _service.SyncAsync(CatalogParserTests.LoadSample(), Config, CancellationToken.None);

            Assert.True(File.Exists(outside));
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public async Task RefusesEmptyCatalog()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SyncAsync(Array.Empty<Arc>(), Config, CancellationToken.None));
    }

    [Fact]
    public async Task KeepsFilesWhenCatalogShrinksDrastically()
    {
        var arcs = CatalogParserTests.LoadSample();
        await _service.SyncAsync(arcs, Config, CancellationToken.None);

        var result = await _service.SyncAsync(arcs.Where(a => a.IsSpecial).ToList(), Config, CancellationToken.None);

        Assert.Equal(0, result.Deleted);
        Assert.True(File.Exists(Path.Combine(_root, "One Pace", "Season 01", "One Pace - S01E01.strm")));
    }

    [Fact]
    public async Task RequiresAbsoluteOutputPath()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SyncAsync(CatalogParserTests.LoadSample(), new PluginConfiguration { OutputPath = "relative/path" }, CancellationToken.None));
    }

    private sealed class NoHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new InvalidOperationException("No HTTP expected in tests.");
    }
}
