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
    internal sealed class Factory(
        AetheryteFunctions aetheryteFunctions,
        GameFunctions gameFunctions,
        ILoggerFactory loggerFactory) : SimpleTaskFactory
    {
        public override ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.AttuneAethernetShard)
                return null;

            ArgumentNullException.ThrowIfNull(step.AethernetShard);

            return new DoAttune(step.AethernetShard.Value, aetheryteFunctions, gameFunctions,
                loggerFactory.CreateLogger<DoAttune>());
        }
    }

    private sealed class DoAttune(
        EAetheryteLocation aetheryteLocation,
        AetheryteFunctions aetheryteFunctions,
        GameFunctions gameFunctions,
        ILogger<DoAttune> logger) : ITask
    {
        public bool Start()
        {
            if (!aetheryteFunctions.IsAetheryteUnlocked(aetheryteLocation))
            {
                logger.LogInformation("Attuning to aethernet shard {AethernetShard}", aetheryteLocation);
                gameFunctions.InteractWith((uint)aetheryteLocation, ObjectKind.Aetheryte);
                return true;
            }

            logger.LogInformation("Already attuned to aethernet shard {AethernetShard}", aetheryteLocation);
            return false;
        }

        public ETaskResult Update() =>
            aetheryteFunctions.IsAetheryteUnlocked(aetheryteLocation)
                ? ETaskResult.TaskComplete
                : ETaskResult.StillRunning;

        public override string ToString() => $"AttuneAethernetShard({aetheryteLocation})";
    }
}
