﻿using System;
using Microsoft.Extensions.Logging;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Common;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Interactions;

internal static class Aetheryte
{
    internal sealed class Factory : SimpleTaskFactory
    {
        public override ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.AttuneAetheryte)
                return null;

            ArgumentNullException.ThrowIfNull(step.Aetheryte);

            return new Attune(step.Aetheryte.Value);
        }
    }

    internal sealed record Attune(EAetheryteLocation AetheryteLocation) : ITask
    {
        public bool ShouldRedoOnInterrupt() => true;
        public override string ToString() => $"共鸣({AetheryteLocation})";
    }

    internal sealed class DoAttune(
        AetheryteFunctions aetheryteFunctions,
        GameFunctions gameFunctions,
        ILogger<DoAttune> logger) : TaskExecutor<Attune>
    {
        protected override bool Start()
        {
            if (!aetheryteFunctions.IsAetheryteUnlocked(Task.AetheryteLocation))
            {
                logger.LogInformation("Attuning to aetheryte {Aetheryte}", Task.AetheryteLocation);
                ProgressContext =
                    InteractionProgressContext.FromActionUseOrDefault(() =>
                        gameFunctions.InteractWith((uint)Task.AetheryteLocation));
                return true;
            }

            logger.LogInformation("Already attuned to aetheryte {Aetheryte}", Task.AetheryteLocation);
            return false;
        }

        public override ETaskResult Update() =>
            aetheryteFunctions.IsAetheryteUnlocked(Task.AetheryteLocation)
                ? ETaskResult.TaskComplete
                : ETaskResult.StillRunning;

        public override bool ShouldInterruptOnDamage() => true;
    }
}
