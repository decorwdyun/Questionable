using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Common;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Interactions;

internal static class Aetheryte
{
    internal sealed class Factory(
        AetheryteFunctions aetheryteFunctions,
        GameFunctions gameFunctions,
        ILoggerFactory loggerFactory) : SimpleTaskFactory
    {
        public override ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.AttuneAetheryte)
                return null;

            ArgumentNullException.ThrowIfNull(step.Aetheryte);

            return new DoAttune(step.Aetheryte.Value, aetheryteFunctions, gameFunctions,
                loggerFactory.CreateLogger<DoAttune>());
        }
    }

    private sealed class DoAttune(
        EAetheryteLocation aetheryteLocation,
        AetheryteFunctions aetheryteFunctions,
        GameFunctions gameFunctions,
        ILogger<DoAttune> logger) : ITask
    {
        private InteractionProgressContext? _progressContext;

        public InteractionProgressContext? ProgressContext() => _progressContext;

        public bool Start()
        {
            if (!aetheryteFunctions.IsAetheryteUnlocked(aetheryteLocation))
            {
                logger.LogInformation("Attuning to aetheryte {Aetheryte}", aetheryteLocation);
                _progressContext =
                    InteractionProgressContext.FromActionUseOrDefault(() =>
                        gameFunctions.InteractWith((uint)aetheryteLocation));
                return true;
            }

            logger.LogInformation("Already attuned to aetheryte {Aetheryte}", aetheryteLocation);
            return false;
        }

        public ETaskResult Update() =>
            aetheryteFunctions.IsAetheryteUnlocked(aetheryteLocation)
                ? ETaskResult.TaskComplete
                : ETaskResult.StillRunning;

        public override string ToString() => $"AttuneAetheryte({aetheryteLocation})";
    }
}
