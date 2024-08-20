using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps.Common;
using Questionable.Data;
using Questionable.External;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Common;
using Questionable.Model.Common.Converter;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Shared;

internal static class AethernetShortcut
{
    internal sealed class Factory(
        MovementController movementController,
        AetheryteFunctions aetheryteFunctions,
        GameFunctions gameFunctions,
        IClientState clientState,
        AetheryteData aetheryteData,
        TerritoryData territoryData,
        LifestreamIpc lifestreamIpc,
        ICondition condition,
        ILoggerFactory loggerFactory)
        : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.AethernetShortcut == null)
                yield break;

            yield return new WaitConditionTask(() => movementController.IsNavmeshReady,
                "Wait(navmesh ready)");
            yield return Use(step.AethernetShortcut.From, step.AethernetShortcut.To,
                step.SkipConditions?.AethernetShortcutIf);
        }

        public ITask Use(EAetheryteLocation from, EAetheryteLocation to, SkipAetheryteCondition? skipConditions = null)
        {
            return new UseAethernetShortcut(from, to, skipConditions ?? new(),
                loggerFactory.CreateLogger<UseAethernetShortcut>(), aetheryteFunctions, gameFunctions, clientState,
                aetheryteData, territoryData, lifestreamIpc, movementController, condition);
        }
    }

    internal sealed class UseAethernetShortcut(
        EAetheryteLocation from,
        EAetheryteLocation to,
        SkipAetheryteCondition skipConditions,
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

        public EAetheryteLocation From => from;
        public EAetheryteLocation To => to;

        public bool Start()
        {
            if (!skipConditions.Never)
            {
                if (skipConditions.InSameTerritory && clientState.TerritoryType == aetheryteData.TerritoryIds[to])
                {
                    logger.LogInformation("Skipping aethernet shortcut because the target is in the same territory");
                    return false;
                }

                if (skipConditions.InTerritory.Contains(clientState.TerritoryType))
                {
                    logger.LogInformation(
                        "Skipping aethernet shortcut because the target is in the specified territory");
                    return false;
                }

                if (skipConditions.AetheryteLocked != null &&
                    !aetheryteFunctions.IsAetheryteUnlocked(skipConditions.AetheryteLocked.Value))
                {
                    logger.LogInformation("Skipping aethernet shortcut because the target aetheryte is locked");
                    return false;
                }

                if (skipConditions.AetheryteUnlocked != null &&
                    aetheryteFunctions.IsAetheryteUnlocked(skipConditions.AetheryteUnlocked.Value))
                {
                    logger.LogInformation("Skipping aethernet shortcut because the target aetheryte is unlocked");
                    return false;
                }
            }

            if (aetheryteFunctions.IsAetheryteUnlocked(from) &&
                aetheryteFunctions.IsAetheryteUnlocked(to))
            {
                ushort territoryType = clientState.TerritoryType;
                Vector3 playerPosition = clientState.LocalPlayer!.Position;

                // closer to the source
                if (aetheryteData.CalculateDistance(playerPosition, territoryType, from) <
                    aetheryteData.CalculateDistance(playerPosition, territoryType, to))
                {
                    if (aetheryteData.CalculateDistance(playerPosition, territoryType, from) <
                        (from.IsFirmamentAetheryte() ? 11f : 4f))
                    {
                        DoTeleport();
                        return true;
                    }
                    else if (from == EAetheryteLocation.SolutionNine)
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
                        movementController.NavigateTo(EMovementType.Quest, (uint)from, closestPoint, false, true,
                            0.25f);
                        return true;
                    }
                    else
                    {
                        if (territoryData.CanUseMount(territoryType) &&
                            aetheryteData.CalculateDistance(playerPosition, territoryType, from) > 30 &&
                            !gameFunctions.HasStatusPreventingMount())
                        {
                            _triedMounting = gameFunctions.Mount();
                            if (_triedMounting)
                            {
                                _continueAt = DateTime.Now.AddSeconds(0.5);
                                return true;
                            }
                        }

                        MoveTo();
                        return true;
                    }
                }
            }
            else
                logger.LogWarning(
                    "Aethernet shortcut not unlocked (from: {FromAetheryte}, to: {ToAetheryte}), walking manually",
                    from, to);

            return false;
        }

        private void MoveTo()
        {
            logger.LogInformation("Moving to aethernet shortcut");
            _moving = true;
            movementController.NavigateTo(EMovementType.Quest, (uint)from, aetheryteData.Locations[from],
                false, true,
                from.IsFirmamentAetheryte()
                    ? 4.4f
                    : AetheryteConverter.IsLargeAetheryte(from)
                        ? 10.9f
                        : 6.9f);
        }

        private void DoTeleport()
        {
            if (from.IsFirmamentAetheryte())
            {
                logger.LogInformation("Using manual teleport interaction");
                _teleported = gameFunctions.InteractWith((uint)from, ObjectKind.EventObj);
            }
            else
            {
                logger.LogInformation("Using lifestream to teleport to {Destination}", to);
                lifestreamIpc.Teleport(to);
                _teleported = true;
            }
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
                    MoveTo();
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
                DoTeleport();
                return ETaskResult.StillRunning;
            }

            if (aetheryteData.IsAirshipLanding(to))
            {
                if (aetheryteData.CalculateAirshipLandingDistance(clientState.LocalPlayer?.Position ?? Vector3.Zero,
                        clientState.TerritoryType, to) > 5)
                    return ETaskResult.StillRunning;
            }
            else if (aetheryteData.IsCityAetheryte(to))
            {
                if (aetheryteData.CalculateDistance(clientState.LocalPlayer?.Position ?? Vector3.Zero,
                        clientState.TerritoryType, to) > 20)
                    return ETaskResult.StillRunning;
            }
            else
            {
                // some overworld location (e.g. 'Tesselation (Lakeland)' would end up here
                if (clientState.TerritoryType != aetheryteData.TerritoryIds[to])
                    return ETaskResult.StillRunning;
            }


            return ETaskResult.TaskComplete;
        }

        public override string ToString() => $"UseAethernet({from} -> {to})";
    }
}
