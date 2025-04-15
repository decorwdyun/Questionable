using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Microsoft.Extensions.Logging;

namespace Questionable.External;

internal sealed class PandorasBoxIpc
{
    private readonly ILogger<PandorasBoxIpc> _logger;
    private readonly ICallGateSubscriber<string,bool?> _getFeatureEnabled;
    private bool _loggedIpcError;

    public PandorasBoxIpc(IDalamudPluginInterface pluginInterface, ILogger<PandorasBoxIpc> logger)
    {
        _logger = logger;
        _getFeatureEnabled = pluginInterface.GetIpcSubscriber<string, bool?>("PandorasBox.GetFeatureEnabled");
        logger.LogInformation("Pandora's Box auto active time maneuver enabled: {IsAtmEnabled}", IsAutoActiveTimeManeuverEnabled);
    }

    public bool IsAutoActiveTimeManeuverEnabled
    {
        get
        {
            try
            {
                return _getFeatureEnabled.InvokeFunc("Auto Active Time Maneuver") == true;
            }
            catch (IpcError e)
            {
                if (!_loggedIpcError)
                {
                    _loggedIpcError = true;
                    _logger.LogWarning(e, "Could not query pandora's box for feature status, probably not installed");
                }
                return false;
            }
        }
    }
}
