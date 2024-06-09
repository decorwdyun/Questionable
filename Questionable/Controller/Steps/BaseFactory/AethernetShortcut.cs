using System;
using System.Numerics;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Data;
using Questionable.External;
using Questionable.Model;
using Questionable.Model.V1;
using Questionable.Model.V1.Converter;

namespace Questionable.Controller.Steps.BaseFactory;

internal static class AethernetShortcut
{
    internal sealed class Factory(IServiceProvider serviceProvider) : ITaskFactory
    {
        public ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.AethernetShortcut == null)
                return null;

            return serviceProvider.GetRequiredService<UseAethernetShortcut>()
                .With(step.AethernetShortcut.From, step.AethernetShortcut.To);
        }
    }

    internal sealed class UseAethernetShortcut(
        ILogger<UseAethernetShortcut> logger,
        GameFunctions gameFunctions,
        IClientState clientState,
        AetheryteData aetheryteData,
        LifestreamIpc lifestreamIpc,
        MovementController movementController) : ITask
    {
        private bool _moving;
        private bool _teleported;

        public EAetheryteLocation From { get; set; }
        public EAetheryteLocation To { get; set; }

        public ITask With(EAetheryteLocation from, EAetheryteLocation to)
        {
            From = from;
            To = to;
            return this;
        }

        public bool Start()
        {
            if (gameFunctions.IsAetheryteUnlocked(From) &&
                gameFunctions.IsAetheryteUnlocked(To))
            {
                ushort territoryType = clientState.TerritoryType;
                Vector3 playerPosition = clientState.LocalPlayer!.Position;

                // closer to the source
                if (aetheryteData.CalculateDistance(playerPosition, territoryType, From) <
                    aetheryteData.CalculateDistance(playerPosition, territoryType, To))
                {
                    if (aetheryteData.CalculateDistance(playerPosition, territoryType, From) < 11)
                    {
                        logger.LogInformation("Using lifestream to teleport to {Destination}", To);
                        lifestreamIpc.Teleport(To);

                        _teleported = true;
                        return true;
                    }
                    else
                    {
                        logger.LogInformation("Moving to aethernet shortcut");
                        _moving = true;
                        movementController.NavigateTo(EMovementType.Quest, (uint)From, aetheryteData.Locations[From],
                            false, true,
                            AetheryteConverter.IsLargeAetheryte(From) ? 10.9f : 6.9f);
                        return true;
                    }
                }
            }
            else
                logger.LogWarning(
                    "Aethernet shortcut not unlocked (from: {FromAetheryte}, to: {ToAetheryte}), walking manually",
                    From, To);

            return false;
        }

        public ETaskResult Update()
        {
            if (_moving)
            {
                var movementStartedAt = movementController.MovementStartedAt;
                if (movementStartedAt == DateTime.MaxValue || movementStartedAt.AddSeconds(2) >= DateTime.Now)
                    return ETaskResult.StillRunning;

                if (!movementController.IsPathfinding && !movementController.IsPathRunning)
                    _moving = false;

                return ETaskResult.StillRunning;
            }

            if (!_teleported)
            {
                logger.LogInformation("Using lifestream to teleport to {Destination}", To);
                lifestreamIpc.Teleport(To);

                _teleported = true;
                return ETaskResult.StillRunning;
            }

            if (aetheryteData.CalculateDistance(clientState.LocalPlayer?.Position ?? Vector3.Zero,
                    clientState.TerritoryType, To) > 11)
                return ETaskResult.StillRunning;

            return ETaskResult.TaskComplete;
        }

        public override string ToString() => $"UseAethernet({From} -> {To})";
    }
}
