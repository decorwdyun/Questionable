using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Questionable.External;

internal sealed class YesAlreadyIpc : IDisposable
{
    private readonly DalamudPluginInterface _pluginInterface;
    private readonly IPluginLog _pluginLog;

    public YesAlreadyIpc(DalamudPluginInterface pluginInterface, IPluginLog pluginLog)
    {
        _pluginInterface = pluginInterface;
        _pluginLog = pluginLog;
    }

    public void DisableYesAlready()
    {
        _pluginLog.Debug("Disabling YesAlready");
        if (_pluginInterface.TryGetData<HashSet<string>>("YesAlready.StopRequests", out var data))
            data.Add(nameof(Questionable));
    }

    public void RestoreYesAlready()
    {
        _pluginLog.Debug("Restoring YesAlready");
        if (_pluginInterface.TryGetData<HashSet<string>>("YesAlready.StopRequests", out var data))
            data.Remove(nameof(Questionable));
    }

    public void Dispose() => RestoreYesAlready();
}
