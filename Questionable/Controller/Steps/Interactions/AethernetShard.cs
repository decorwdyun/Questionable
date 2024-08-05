using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Common;
using Questionable.Model.Questing;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace Questionable.Controller.Steps.Interactions;

internal static class AethernetShard
{
    internal sealed class Factory(IServiceProvider serviceProvider) : ITaskFactory
    {
        public ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.AttuneAethernetShard)
                return null;

            ArgumentNullException.ThrowIfNull(step.AethernetShard);

            return serviceProvider.GetRequiredService<DoAttune>()
                .With(step.AethernetShard.Value);
        }
    }

    internal sealed class DoAttune(GameFunctions gameFunctions, ILogger<DoAttune> logger) : ITask
    {
        public EAetheryteLocation AetheryteLocation { get; set; }

        public ITask With(EAetheryteLocation aetheryteLocation)
        {
            AetheryteLocation = aetheryteLocation;
            return this;
        }

        public bool Start()
        {
            if (!gameFunctions.IsAetheryteUnlocked(AetheryteLocation))
            {
                logger.LogInformation("Attuning to aethernet shard {AethernetShard}", AetheryteLocation);
                gameFunctions.InteractWith((uint)AetheryteLocation, ObjectKind.Aetheryte);
                return true;
            }

            logger.LogInformation("Already attuned to aethernet shard {AethernetShard}", AetheryteLocation);
            return false;
        }

        public ETaskResult Update() =>
            gameFunctions.IsAetheryteUnlocked(AetheryteLocation)
                ? ETaskResult.TaskComplete
                : ETaskResult.StillRunning;

        public override string ToString() => $"AttuneAethernetShard({AetheryteLocation})";
    }
}
