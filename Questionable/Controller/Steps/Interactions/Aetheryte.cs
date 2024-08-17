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
    internal sealed class Factory(IServiceProvider serviceProvider) : SimpleTaskFactory
    {
        public override ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.AttuneAetheryte)
                return null;

            ArgumentNullException.ThrowIfNull(step.Aetheryte);

            return serviceProvider.GetRequiredService<DoAttune>()
                .With(step.Aetheryte.Value);
        }
    }

    internal sealed class DoAttune(
        AetheryteFunctions aetheryteFunctions,
        GameFunctions gameFunctions,
        ILogger<DoAttune> logger) : ITask
    {
        public EAetheryteLocation AetheryteLocation { get; set; }

        public ITask With(EAetheryteLocation aetheryteLocation)
        {
            AetheryteLocation = aetheryteLocation;
            return this;
        }

        public bool Start()
        {
            if (!aetheryteFunctions.IsAetheryteUnlocked(AetheryteLocation))
            {
                logger.LogInformation("Attuning to aetheryte {Aetheryte}", AetheryteLocation);
                gameFunctions.InteractWith((uint)AetheryteLocation);
                return true;
            }

            logger.LogInformation("Already attuned to aetheryte {Aetheryte}", AetheryteLocation);
            return false;
        }

        public ETaskResult Update() =>
            aetheryteFunctions.IsAetheryteUnlocked(AetheryteLocation)
                ? ETaskResult.TaskComplete
                : ETaskResult.StillRunning;

        public override string ToString() => $"AttuneAetheryte({AetheryteLocation})";
    }
}
