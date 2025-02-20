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
using Questionable.External;

namespace Questionable.Controller.CombatModules;

internal sealed class BossModModule : ICombatModule, IDisposable
{
    private readonly ILogger<BossModModule> _logger;
    private readonly BossModIpc _bossModIpc;
    private readonly Configuration _configuration;

    private static Stream Preset => typeof(BossModModule).Assembly.GetManifestResourceStream("Questionable.Controller.CombatModules.BossModPreset")!;

    public BossModModule(
        ILogger<BossModModule> logger,
        BossModIpc bossModIpc,
        Configuration configuration)
    {
        _logger = logger;
        _bossModIpc = bossModIpc;
        _configuration = configuration;
    }

    public bool CanHandleFight(CombatController.CombatData combatData)
    {
        if (_configuration.General.CombatModule != Configuration.ECombatModule.BossMod)
            return false;

        return _bossModIpc.IsSupported();
    }

    public bool Start(CombatController.CombatData combatData)
    {
        try
        {
            if (_bossModIpc.GetPreset("Questionable") == null)
            {
                using var reader = new StreamReader(Preset);
                _logger.LogInformation("Loading Questionable BossMod Preset: {LoadedState}", _bossModIpc.CreatePreset(reader.ReadToEnd(), true));
            }
            _bossModIpc.SetPreset("Questionable");
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
            _bossModIpc.ClearPreset();
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
