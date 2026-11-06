using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.OnePaceStreams.Catalog;

/// <summary>Downloads and parses the One Pace catalog.</summary>
public sealed class OnePaceCatalogClient(IHttpClientFactory httpClientFactory, ILogger<OnePaceCatalogClient> logger)
{
    /// <summary>The page listing all arcs, episodes and Pixeldrain links.</summary>
    public static readonly Uri WatchPageUri = new("https://onepace.net/en/watch");

    /// <summary>Fetches all arcs.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The arcs, in page order.</returns>
    public async Task<IReadOnlyList<Arc>> GetArcsAsync(CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(NamedClient.Default);
        var html = await client.GetStringAsync(WatchPageUri, cancellationToken).ConfigureAwait(false);
        var arcs = OnePaceCatalogParser.Parse(html, WatchPageUri);
        logger.LogInformation(
            "One Pace catalog: {Arcs} arcs, {Episodes} episodes",
            arcs.Count,
            arcs.Sum(a => a.Episodes.Count));
        return arcs;
    }
}
