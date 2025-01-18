using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using Json.Schema;
using Microsoft.Extensions.Logging;
using Questionable.Model;
using System;
using System.IO;
using System.Numerics;

namespace Questionable.Controller.CombatModules;

internal sealed class BossModModule : ICombatModule, IDisposable
{
    private const string Name = "BossMod";
    private readonly ILogger<BossModModule> _logger;
    private readonly Configuration _configuration;
    private readonly ICallGateSubscriber<string, string?> _getPreset;
    private readonly ICallGateSubscriber<string, bool, bool> _createPreset;
    private readonly ICallGateSubscriber<string, bool> _setPreset;
    private readonly ICallGateSubscriber<bool> _clearPreset;

    private static Stream Preset => typeof(BossModModule).Assembly.GetManifestResourceStream("Questionable.Controller.CombatModules.BossModPreset")!;

    public BossModModule(
        ILogger<BossModModule> logger,
        IDalamudPluginInterface pluginInterface,
        Configuration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        _getPreset = pluginInterface.GetIpcSubscriber<string, string?>($"{Name}.Presets.Get");
        _createPreset = pluginInterface.GetIpcSubscriber<string, bool, bool>($"{Name}.Presets.Create");
        _setPreset = pluginInterface.GetIpcSubscriber<string, bool>($"{Name}.Presets.SetActive");
        _clearPreset = pluginInterface.GetIpcSubscriber<bool>($"{Name}.Presets.ClearActive");
    }

    public bool CanHandleFight(CombatController.CombatData combatData)
    {
        if (_configuration.General.CombatModule != Configuration.ECombatModule.BossMod)
            return false;

        try
        {
            return _getPreset.HasFunction;
        }
        catch (IpcError)
        {
            return false;
        }
    }

    public bool Start(CombatController.CombatData combatData)
    {
        try
        {
            if (_getPreset.InvokeFunc("Questionable") == null)
            {
                using var reader = new StreamReader(Preset);
                _logger.LogInformation("Loading Questionable BossMod Preset: {LoadedState}", _createPreset.InvokeFunc(reader.ReadToEnd(), true));
            }
            _setPreset.InvokeFunc("Questionable");
            return true;
        }
        catch (IpcError e)
        {
            _logger.LogWarning(e, "Could not start combat");
            return false;
        }
    }

    public bool Stop()
    {
        try
        {
            _clearPreset.InvokeFunc();
            return true;
        }
        catch (IpcError e)
        {
            _logger.LogWarning(e, "Could not turn off combat");
            return false;
        }
    }

    public void Update(IGameObject gameObject)
    {
    }

    public bool CanAttack(IBattleNpc target) => true;

    public void Dispose() => Stop();
}
