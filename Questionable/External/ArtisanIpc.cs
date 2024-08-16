using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Microsoft.Extensions.Logging;

namespace Questionable.External;

internal sealed class ArtisanIpc
{
    private readonly ILogger<ArtisanIpc> _logger;
    private readonly ICallGateSubscriber<ushort, int, object> _craftItem;

    public ArtisanIpc(IDalamudPluginInterface pluginInterface, ILogger<ArtisanIpc> logger)
    {
        _logger = logger;
        _craftItem = pluginInterface.GetIpcSubscriber<ushort, int, object>("Artisan.CraftItem");
    }

    public bool CraftItem(ushort recipeId, int quantity)
    {
        try
        {
            _logger.LogInformation("Attempting to craft {Quantity} items with recipe {RecipeId} with Artisan", quantity,
                recipeId);
            _craftItem.InvokeAction(recipeId, quantity);
            return true;
        }
        catch (IpcError e)
        {
            _logger.LogError(e, "Unable to craft items");
            return false;
        }
    }
}
