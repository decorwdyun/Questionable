using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Questionable.External;

internal sealed class YesAlreadyIpc : IDisposable
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly IPluginLog _pluginLog;

    public YesAlreadyIpc(IDalamudPluginInterface pluginInterface, IPluginLog pluginLog)
    {
        _pluginInterface = pluginInterface;
        _pluginLog = pluginLog;
    }

    public void DisableYesAlready()
    {
        if (_pluginInterface.TryGetData<HashSet<string>>("YesAlready.StopRequests", out var data) &&
            !data.Contains(nameof(Questionable)))
        {
            _pluginLog.Debug("Disabling YesAlready");
            data.Add(nameof(Questionable));
        }
    }

    public void RestoreYesAlready()
    {
        if (_pluginInterface.TryGetData<HashSet<string>>("YesAlready.StopRequests", out var data) &&
            data.Contains(nameof(Questionable)))
        {
            _pluginLog.Debug("Restoring YesAlready");
            data.Remove(nameof(Questionable));
        }
    }

    public void Dispose() => RestoreYesAlready();
}
