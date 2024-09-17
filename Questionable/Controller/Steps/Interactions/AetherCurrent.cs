using System;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Data;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Interactions;

internal static class AetherCurrent
{
    internal sealed class Factory(
        GameFunctions gameFunctions,
        AetherCurrentData aetherCurrentData,
        IChatGui chatGui,
        ILoggerFactory loggerFactory) : SimpleTaskFactory
    {
        public override ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.AttuneAetherCurrent)
                return null;

            ArgumentNullException.ThrowIfNull(step.DataId);
            ArgumentNullException.ThrowIfNull(step.AetherCurrentId);

            if (!aetherCurrentData.IsValidAetherCurrent(step.TerritoryId, step.AetherCurrentId.Value))
            {
                chatGui.PrintError(
                    $"[Questionable] Aether current with id {step.AetherCurrentId} is referencing an invalid aether current, will skip attunement");
                return null;
            }

            return new DoAttune(step.DataId.Value, step.AetherCurrentId.Value, gameFunctions,
                loggerFactory.CreateLogger<DoAttune>());
        }
    }

    private sealed class DoAttune(
        uint dataId,
        uint aetherCurrentId,
        GameFunctions gameFunctions,
        ILogger<DoAttune> logger) : ITask
    {
        private InteractionProgressContext? _progressContext;

        public InteractionProgressContext? ProgressContext() => _progressContext;

        public bool Start()
        {
            if (!gameFunctions.IsAetherCurrentUnlocked(aetherCurrentId))
            {
                logger.LogInformation("Attuning to aether current {AetherCurrentId} / {DataId}", aetherCurrentId,
                    dataId);
                _progressContext =
                    InteractionProgressContext.FromActionUseOrDefault(() => gameFunctions.InteractWith(dataId));
                return true;
            }

            logger.LogInformation("Already attuned to aether current {AetherCurrentId} / {DataId}", aetherCurrentId,
                dataId);
            return false;
        }

        public ETaskResult Update() =>
            gameFunctions.IsAetherCurrentUnlocked(aetherCurrentId)
                ? ETaskResult.TaskComplete
                : ETaskResult.StillRunning;

        public override string ToString() => $"AttuneAetherCurrent({aetherCurrentId})";
    }
}
