using System;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Data;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Interactions;

internal static class AetherCurrent
{
    internal sealed class Factory(IServiceProvider serviceProvider, AetherCurrentData aetherCurrentData, IChatGui chatGui) : ITaskFactory
    {
        public ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.AttuneAetherCurrent)
                return null;

            ArgumentNullException.ThrowIfNull(step.DataId);
            ArgumentNullException.ThrowIfNull(step.AetherCurrentId);

            if (!aetherCurrentData.IsValidAetherCurrent(step.TerritoryId, step.AetherCurrentId.Value))
            {
                chatGui.PrintError($"[Questionable] Aether current with id {step.AetherCurrentId} is referencing an invalid aether current, will skip attunement");
                return null;
            }

            return serviceProvider.GetRequiredService<DoAttune>()
                .With(step.DataId.Value, step.AetherCurrentId.Value);
        }
    }

    internal sealed class DoAttune(GameFunctions gameFunctions, ILogger<DoAttune> logger) : ITask
    {
        public uint DataId { get; set; }
        public uint AetherCurrentId { get; set; }

        public ITask With(uint dataId, uint aetherCurrentId)
        {
            DataId = dataId;
            AetherCurrentId = aetherCurrentId;
            return this;
        }

        public bool Start()
        {
            if (!gameFunctions.IsAetherCurrentUnlocked(AetherCurrentId))
            {
                logger.LogInformation("Attuning to aether current {AetherCurrentId} / {DataId}", AetherCurrentId,
                    DataId);
                gameFunctions.InteractWith(DataId);
                return true;
            }

            logger.LogInformation("Already attuned to aether current {AetherCurrentId} / {DataId}", AetherCurrentId,
                DataId);
            return false;
        }

        public ETaskResult Update() =>
            gameFunctions.IsAetherCurrentUnlocked(AetherCurrentId)
                ? ETaskResult.TaskComplete
                : ETaskResult.StillRunning;

        public override string ToString() => $"AttuneAetherCurrent({AetherCurrentId})";
    }
}
