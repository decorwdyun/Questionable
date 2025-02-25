using System;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;

namespace Questionable.Controller.CombatModules;

internal sealed class AeAssistModule : ICombatModule, IDisposable
{
    private readonly ILogger<AeAssistModule> _logger;
    private readonly Configuration _configuration;
    private readonly ICommandManager _commandManager;

    public AeAssistModule(ILogger<AeAssistModule> logger, Configuration configuration,
        ICommandManager commandManager)
    {
        _logger = logger;
        _configuration = configuration;
        _commandManager = commandManager;
    }
    public bool CanHandleFight(CombatController.CombatData combatData)
    {
        return _configuration.General.CombatModule == Configuration.ECombatModule.AEAssist;
    }

    public bool Start(CombatController.CombatData combatData)
    {
        _logger.LogInformation("Starting AE Assist module");
        _commandManager.ProcessCommand("/aepull on");
        return true;
    }

    public bool Stop()
    {
        _logger.LogInformation("Stopping AE Assist module");
        _commandManager.ProcessCommand("/aepull off");
        return true;
    }

    public void Update(IGameObject nextTarget) {}

    public bool CanAttack(IBattleNpc target) => true;

    public void Dispose() => Stop();
}