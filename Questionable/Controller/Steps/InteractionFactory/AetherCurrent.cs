using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Controller.Steps.InteractionFactory;

internal static class AetherCurrent
{
    internal sealed class Factory(IServiceProvider serviceProvider) : ITaskFactory
    {
        public ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.AttuneAetherCurrent)
                return null;

            ArgumentNullException.ThrowIfNull(step.DataId);
            ArgumentNullException.ThrowIfNull(step.AetherCurrentId);

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

            logger.LogInformation("Already attuned to aether current {AetherCurrentId} / {DataId}", AetherCurrentId, DataId);
            return false;
        }

        public ETaskResult Update() =>
            gameFunctions.IsAetherCurrentUnlocked(AetherCurrentId)
                ? ETaskResult.TaskComplete
                : ETaskResult.StillRunning;

        public override string ToString() => $"AttuneAetherCurrent({AetherCurrentId})";
    }
}
