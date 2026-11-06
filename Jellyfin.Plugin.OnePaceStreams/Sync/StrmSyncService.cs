using System.Text.Json;
using Jellyfin.Plugin.OnePaceStreams.Catalog;
using Jellyfin.Plugin.OnePaceStreams.Configuration;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.OnePaceStreams.Sync;

/// <summary>Outcome of a sync run.</summary>
public sealed record SyncResult(int Written, int Unchanged, int Deleted, int Episodes, int SkippedEpisodes)
{
    /// <summary>Gets a value indicating whether anything on disk changed.</summary>
    public bool Changed => Written > 0 || Deleted > 0;
}

/// <summary>Writes the .strm/.nfo/artwork layout and removes files it previously wrote.</summary>
public sealed class StrmSyncService(IHttpClientFactory httpClientFactory, ILogger<StrmSyncService> logger)
{
    /// <summary>Manifest of files written by this plugin, stored in the output folder.</summary>
    public const string ManifestFileName = ".onepace-manifest.json";

    /// <summary>Applies the catalog to the output folder.</summary>
    /// <param name="arcs">Arcs from the catalog.</param>
    /// <param name="config">Settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result.</returns>
    public async Task<SyncResult> SyncAsync(IReadOnlyList<Arc> arcs, PluginConfiguration config, CancellationToken cancellationToken)
    {
        var root = ResolveRoot(config.OutputPath);
        var plan = LibraryPlanner.Build(arcs, config);
        if (plan.EpisodeCount == 0)
        {
            throw new InvalidOperationException("The catalog yielded no playable episodes; refusing to sync so the existing library is kept.");
        }

        var previous = LoadManifest(root);
        var desired = new HashSet<string>(StringComparer.Ordinal);
        int written = 0, unchanged = 0, deleted = 0;

        foreach (var file in plan.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            desired.Add(file.RelativePath);
            if (await WriteIfChangedAsync(Path.Combine(root, file.RelativePath), file.Content, cancellationToken).ConfigureAwait(false))
            {
                written++;
            }
            else
            {
                unchanged++;
            }
        }

        HttpClient? client = null;
        foreach (var image in plan.Images)
        {
            var path = Path.Combine(root, image.RelativePath);
            if (!File.Exists(path))
            {
                client ??= httpClientFactory.CreateClient(NamedClient.Default);
                if (await TryDownloadAsync(client, image.Source, path, cancellationToken).ConfigureAwait(false))
                {
                    written++;
                }
            }

            if (File.Exists(path))
            {
                desired.Add(image.RelativePath);
            }
        }

        var stale = previous.Where(p => !desired.Contains(p)).ToList();
        var previousEpisodes = previous.Count(p => p.EndsWith(".strm", StringComparison.Ordinal));
        if (stale.Count > 0 && plan.EpisodeCount < previousEpisodes / 2)
        {
            logger.LogWarning(
                "Catalog shrank from {Previous} to {Current} episodes; not deleting {Stale} stale files this run",
                previousEpisodes,
                plan.EpisodeCount,
                stale.Count);
            desired.UnionWith(stale);
        }
        else
        {
            foreach (var relative in stale)
            {
                if (DeleteOwnedFile(root, relative))
                {
                    deleted++;
                }
            }
        }

        await SaveManifestAsync(root, desired, cancellationToken).ConfigureAwait(false);
        return new SyncResult(written, unchanged, deleted, plan.EpisodeCount, plan.SkippedEpisodes);
    }

    private static string ResolveRoot(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath) || !Path.IsPathFullyQualified(outputPath))
        {
            throw new InvalidOperationException("Set an absolute output path in the One Pace Streams plugin settings.");
        }

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputPath));
        Directory.CreateDirectory(root);
        return root;
    }

    private static async Task<bool> WriteIfChangedAsync(string path, string content, CancellationToken cancellationToken)
    {
        if (File.Exists(path) && await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false) == content)
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        await File.WriteAllTextAsync(temp, content, cancellationToken).ConfigureAwait(false);
        File.Move(temp, path, overwrite: true);
        return true;
    }

    private async Task<bool> TryDownloadAsync(HttpClient client, Uri source, string path, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, source);
            request.Headers.Accept.ParseAdd("image/jpeg");
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Artwork download {Url} failed with {Status}", source, response.StatusCode);
                return false;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = path + ".tmp";
            await File.WriteAllBytesAsync(temp, bytes, cancellationToken).ConfigureAwait(false);
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Artwork download {Url} failed", source);
            return false;
        }
    }

    private bool DeleteOwnedFile(string root, string relative)
    {
        var full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            logger.LogWarning("Ignoring manifest entry outside the output folder: {Path}", relative);
            return false;
        }

        if (!File.Exists(full))
        {
            return false;
        }

        File.Delete(full);
        var directory = Path.GetDirectoryName(full);
        while (directory is not null && directory.Length > root.Length && !Directory.EnumerateFileSystemEntries(directory).Any())
        {
            Directory.Delete(directory);
            directory = Path.GetDirectoryName(directory);
        }

        return true;
    }

    private HashSet<string> LoadManifest(string root)
    {
        var path = Path.Combine(root, ManifestFileName);
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(path)) ?? [];
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Ignoring unreadable manifest {Path}", path);
            return [];
        }
    }

    private static async Task SaveManifestAsync(string root, IEnumerable<string> entries, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(entries.Order(StringComparer.Ordinal).ToList(), ManifestJsonOptions);
        await WriteIfChangedAsync(Path.Combine(root, ManifestFileName), json, cancellationToken).ConfigureAwait(false);
    }

    private static readonly JsonSerializerOptions ManifestJsonOptions = new() { WriteIndented = true };
}
