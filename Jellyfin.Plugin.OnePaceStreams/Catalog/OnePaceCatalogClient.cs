using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.OnePaceStreams.Catalog;

/// <summary>Downloads and parses the One Pace catalog.</summary>
public sealed class OnePaceCatalogClient(IHttpClientFactory httpClientFactory, ILogger<OnePaceCatalogClient> logger)
{
    /// <summary>The page listing all arcs, episodes and Pixeldrain links.</summary>
    public static readonly Uri WatchPageUri = new("https://onepace.net/en/watch");

    /// <summary>Fetches the catalog.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The arcs, in page order, and the logo.</returns>
    public async Task<OnePaceCatalog> GetCatalogAsync(CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(NamedClient.Default);
        var html = await client.GetStringAsync(WatchPageUri, cancellationToken).ConfigureAwait(false);
        var catalog = OnePaceCatalogParser.Parse(html, WatchPageUri);
        logger.LogInformation(
            "One Pace catalog: {Arcs} arcs, {Episodes} episodes",
            catalog.Arcs.Count,
            catalog.Arcs.Sum(a => a.Episodes.Count));
        return catalog;
    }
}
