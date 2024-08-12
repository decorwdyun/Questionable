using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Data;
using Questionable.External;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Common;
using Questionable.Model.Common.Converter;
using Questionable.Model.Questing;
using Questionable.Model.Questing.Converter;

namespace Questionable.Controller.Steps.Shared;

internal static class AethernetShortcut
{
    internal sealed class Factory(IServiceProvider serviceProvider) : ITaskFactory
    {
        public ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.AethernetShortcut == null)
                return null;

            return serviceProvider.GetRequiredService<UseAethernetShortcut>()
                .With(step.AethernetShortcut.From, step.AethernetShortcut.To, step.SkipConditions?.AethernetShortcutIf);
        }
    }

    internal sealed class UseAethernetShortcut(
        ILogger<UseAethernetShortcut> logger,
        AetheryteFunctions aetheryteFunctions,
        GameFunctions gameFunctions,
        IClientState clientState,
        AetheryteData aetheryteData,
        TerritoryData territoryData,
        LifestreamIpc lifestreamIpc,
        MovementController movementController,
        ICondition condition) : ISkippableTask
    {
        private bool _moving;
        private bool _teleported;
        private bool _triedMounting;
        private DateTime _continueAt = DateTime.MinValue;

        public EAetheryteLocation From { get; set; }
        public EAetheryteLocation To { get; set; }
        public SkipAetheryteCondition SkipConditions { get; set; } = null!;

        public ITask With(EAetheryteLocation from, EAetheryteLocation to,
            SkipAetheryteCondition? skipConditions = null)
        {
            From = from;
            To = to;
            SkipConditions = skipConditions ?? new();
            return this;
        }

        public bool Start()
        {
            if (!SkipConditions.Never)
            {
                if (SkipConditions.InSameTerritory && clientState.TerritoryType == aetheryteData.TerritoryIds[To])
                {
                    logger.LogInformation("Skipping aethernet shortcut because the target is in the same territory");
                    return false;
                }

                if (SkipConditions.InTerritory.Contains(clientState.TerritoryType))
                {
                    logger.LogInformation(
                        "Skipping aethernet shortcut because the target is in the specified territory");
                    return false;
                }

                if (SkipConditions.AetheryteLocked != null &&
                    !aetheryteFunctions.IsAetheryteUnlocked(SkipConditions.AetheryteLocked.Value))
                {
                    logger.LogInformation("Skipping aethernet shortcut because the target aetheryte is locked");
                    return false;
                }

                if (SkipConditions.AetheryteUnlocked != null &&
                    aetheryteFunctions.IsAetheryteUnlocked(SkipConditions.AetheryteUnlocked.Value))
                {
                    logger.LogInformation("Skipping aethernet shortcut because the target aetheryte is unlocked");
                    return false;
                }
            }

            if (aetheryteFunctions.IsAetheryteUnlocked(From) &&
                aetheryteFunctions.IsAetheryteUnlocked(To))
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
                    else if (From == EAetheryteLocation.SolutionNine)
                    {
                        logger.LogInformation("Moving to S9 aetheryte");
                        List<Vector3> nearbyPoints =
                        [
                            new(7.225532f, 8.467899f, -7.1670876f),
                            new(7.177844f, 8.467899f, 7.2216787f),
                            new(-7.0762224f, 8.467898f, 7.1924725f),
                            new(-7.1289554f, 8.467898f, -7.0594683f)
                        ];

                        Vector3 closestPoint = nearbyPoints.MinBy(x => (playerPosition - x).Length());
                        _moving = true;
                        movementController.NavigateTo(EMovementType.Quest, (uint)From, closestPoint, false, true,
                            0.25f);
                        return true;
                    }
                    else
                    {
                        if (territoryData.CanUseMount(territoryType) &&
                            aetheryteData.CalculateDistance(playerPosition, territoryType, From) > 30 &&
                            !gameFunctions.HasStatusPreventingMount())
                        {
                            _triedMounting = gameFunctions.Mount();
                            if (_triedMounting)
                            {
                                _continueAt = DateTime.Now.AddSeconds(0.5);
                                return true;
                            }
                        }

                        MoveTo(From);
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

        private void MoveTo(EAetheryteLocation from)
        {
            logger.LogInformation("Moving to aethernet shortcut");
            _moving = true;
            movementController.NavigateTo(EMovementType.Quest, (uint)From, aetheryteData.Locations[From],
                false, true,
                AetheryteConverter.IsLargeAetheryte(From) ? 10.9f : 6.9f);
        }

        public ETaskResult Update()
        {
            if (DateTime.Now < _continueAt)
                return ETaskResult.StillRunning;

            if (_triedMounting)
            {
                if (condition[ConditionFlag.Mounted])
                {
                    _triedMounting = false;
                    MoveTo(From);
                    return ETaskResult.StillRunning;
                }
                else
                    return ETaskResult.StillRunning;
            }

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

            if (aetheryteData.IsAirshipLanding(To))
            {
                if (aetheryteData.CalculateAirshipLandingDistance(clientState.LocalPlayer?.Position ?? Vector3.Zero,
                        clientState.TerritoryType, To) > 5)
                    return ETaskResult.StillRunning;
            }
            else if (aetheryteData.IsCityAetheryte(To))
            {
                if (aetheryteData.CalculateDistance(clientState.LocalPlayer?.Position ?? Vector3.Zero,
                        clientState.TerritoryType, To) > 20)
                    return ETaskResult.StillRunning;
            }
            else
            {
                // some overworld location (e.g. 'Tesselation (Lakeland)' would end up here
                if (clientState.TerritoryType != aetheryteData.TerritoryIds[To])
                    return ETaskResult.StillRunning;
            }


            return ETaskResult.TaskComplete;
        }

        public override string ToString() => $"UseAethernet({From} -> {To})";
    }
}
