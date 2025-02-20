using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;

namespace Questionable.External;

internal sealed class BossModIpc
{
    private readonly ICommandManager _commandManager;
    private const string Name = "BossMod";

    private readonly ICallGateSubscriber<string, string?> _getPreset;
    private readonly ICallGateSubscriber<string, bool, bool> _createPreset;
    private readonly ICallGateSubscriber<string, bool> _setPreset;
    private readonly ICallGateSubscriber<bool> _clearPreset;

    public BossModIpc(IDalamudPluginInterface pluginInterface, ICommandManager commandManager)
    {
        _commandManager = commandManager;
        _getPreset = pluginInterface.GetIpcSubscriber<string, string?>($"{Name}.Presets.Get");
        _createPreset = pluginInterface.GetIpcSubscriber<string, bool, bool>($"{Name}.Presets.Create");
        _setPreset = pluginInterface.GetIpcSubscriber<string, bool>($"{Name}.Presets.SetActive");
        _clearPreset = pluginInterface.GetIpcSubscriber<bool>($"{Name}.Presets.ClearActive");
    }

    public bool IsSupported()
    {
        try
        {
            return _getPreset.HasFunction;
        }
        catch (IpcError)
        {
            return false;
        }
    }

    public string? GetPreset(string name)
    {
        return _getPreset.InvokeFunc(name);
    }

    public bool CreatePreset(string name, bool overwrite)
    {
        return _createPreset.InvokeFunc(name, overwrite);
    }

    public void SetPreset(string name)
    {
        _setPreset.InvokeFunc(name);
    }

    public void ClearPreset()
    {
        _clearPreset.InvokeFunc();
    }

    // TODO this should use your actual rotation plugin, not always vbm
    public void EnableAi(string presetName = "VBM Default")
    {
        _commandManager.ProcessCommand("/vbmai on");
        _commandManager.ProcessCommand("/vbm cfg ZoneModuleConfig EnableQuestBattles true");
        SetPreset(presetName);
    }

    public void DisableAi()
    {
        _commandManager.ProcessCommand("/vbmai off");
        _commandManager.ProcessCommand("/vbm cfg ZoneModuleConfig EnableQuestBattles false");
        ClearPreset();
    }
}
