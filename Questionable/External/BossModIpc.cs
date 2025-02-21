using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using Questionable.Data;
using Questionable.Model.Questing;

namespace Questionable.External;

internal sealed class BossModIpc
{
    private const string Name = "BossMod";

    private readonly Configuration _configuration;
    private readonly ICommandManager _commandManager;
    private readonly TerritoryData _territoryData;
    private readonly ICallGateSubscriber<string, string?> _getPreset;
    private readonly ICallGateSubscriber<string, bool, bool> _createPreset;
    private readonly ICallGateSubscriber<string, bool> _setPreset;
    private readonly ICallGateSubscriber<bool> _clearPreset;

    public BossModIpc(
        IDalamudPluginInterface pluginInterface,
        Configuration configuration,
        ICommandManager commandManager,
        TerritoryData territoryData)
    {
        _configuration = configuration;
        _commandManager = commandManager;
        _territoryData = territoryData;

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

    public bool IsConfiguredToRunSoloInstance(ElementId questId, SinglePlayerDutyOptions? dutyOptions)
    {
        if (!IsSupported())
            return false;

        if (!_configuration.SinglePlayerDuties.RunSoloInstancesWithBossMod)
            return false;

        dutyOptions ??= new();
        if (!_territoryData.TryGetContentFinderConditionForSoloInstance(questId, dutyOptions.Index, out var cfcData))
            return false;

        if (_configuration.SinglePlayerDuties.BlacklistedSinglePlayerDutyCfcIds.Contains(cfcData.ContentFinderConditionId))
            return false;

        if (_configuration.SinglePlayerDuties.WhitelistedSinglePlayerDutyCfcIds.Contains(cfcData.ContentFinderConditionId))
            return true;

        return dutyOptions.Enabled;
    }
}
