using System;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;

namespace Questionable.Controller.CombatModules;

internal sealed class AeAssistModule(
    ILogger<AeAssistModule> logger,
    Configuration configuration,
    ICommandManager commandManager)
    : ICombatModule, IDisposable
{
    public bool CanHandleFight(CombatController.CombatData combatData)
    {
        return configuration.General.CombatModule == Configuration.ECombatModule.AEAssist;
    }

    public bool Start(CombatController.CombatData combatData)
    {
        logger.LogInformation("Starting AEAssist module");
        commandManager.ProcessCommand("/aeTargetSelector off");
        commandManager.ProcessCommand("/aestop off");
        commandManager.ProcessCommand("/aepull on");
        return true;
    }

    public bool Stop()
    {
        logger.LogInformation("Stopping AEAssist module");
        commandManager.ProcessCommand("/aepull off");
        commandManager.ProcessCommand("/aestop on");
        return true;
    }

    public void Update(IGameObject nextTarget) {}

    public bool CanAttack(IBattleNpc target) => true;

    public void Dispose() => Stop();
}