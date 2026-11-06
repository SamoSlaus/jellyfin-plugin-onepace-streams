using Jellyfin.Plugin.OnePaceStreams.Catalog;
using Jellyfin.Plugin.OnePaceStreams.Sync;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.OnePaceStreams;

/// <summary>Registers plugin services.</summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<OnePaceCatalogClient>();
        serviceCollection.AddSingleton<StrmSyncService>();
    }
}
