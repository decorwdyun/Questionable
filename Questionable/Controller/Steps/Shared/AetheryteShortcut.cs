using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Application.Network.WorkDefinitions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps.Common;
using Questionable.Controller.Utils;
using Questionable.Data;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Common;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Shared;

internal static class AetheryteShortcut
{
    internal sealed class Factory(
        IServiceProvider serviceProvider,
        AetheryteData aetheryteData) : SimpleTaskFactory
    {
        public override ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.AetheryteShortcut == null)
                return null;

            return serviceProvider.GetRequiredService<UseAetheryteShortcut>()
                .With(step, quest.Id, step.AetheryteShortcut.Value,
                    aetheryteData.TerritoryIds[step.AetheryteShortcut.Value]);
        }
    }

    internal sealed class UseAetheryteShortcut(
        ILogger<UseAetheryteShortcut> logger,
        AetheryteFunctions aetheryteFunctions,
        QuestFunctions questFunctions,
        IClientState clientState,
        IChatGui chatGui,
        AetheryteData aetheryteData) : ISkippableTask
    {
        private bool _teleported;
        private DateTime _continueAt;

        public QuestStep? Step { get; set; }
        public ElementId? ElementId { get; set; }
        public EAetheryteLocation TargetAetheryte { get; set; }

        /// <summary>
        /// If using an aethernet shortcut after, the aetheryte's territory-id and the step's territory-id can differ,
        /// we always use the aetheryte's territory-id.
        /// </summary>
        public ushort ExpectedTerritoryId { get; set; }

        public ITask With(QuestStep? step, ElementId? elementId, EAetheryteLocation targetAetheryte,
            ushort expectedTerritoryId)
        {
            Step = step;
            ElementId = elementId;
            TargetAetheryte = targetAetheryte;
            ExpectedTerritoryId = expectedTerritoryId;
            return this;
        }

        public bool Start() => !ShouldSkipTeleport();

        public ETaskResult Update()
        {
            if (DateTime.Now < _continueAt)
                return ETaskResult.StillRunning;

            if (!_teleported)
            {
                _teleported = DoTeleport();
                return ETaskResult.StillRunning;
            }

            if (clientState.TerritoryType == ExpectedTerritoryId)
                return ETaskResult.TaskComplete;

            return ETaskResult.StillRunning;
        }

        private bool ShouldSkipTeleport()
        {
            ushort territoryType = clientState.TerritoryType;
            if (Step != null)
            {
                var skipConditions = Step.SkipConditions?.AetheryteShortcutIf ?? new();
                if (skipConditions is { Never: false })
                {
                    if (skipConditions.InTerritory.Contains(territoryType))
                    {
                        logger.LogInformation("Skipping aetheryte teleport due to SkipCondition (InTerritory)");
                        return true;
                    }

                    if (skipConditions.AetheryteLocked != null &&
                        !aetheryteFunctions.IsAetheryteUnlocked(skipConditions.AetheryteLocked.Value))
                    {
                        logger.LogInformation("Skipping aetheryte teleport due to SkipCondition (AetheryteLocked)");
                        return true;
                    }

                    if (skipConditions.AetheryteUnlocked != null &&
                        aetheryteFunctions.IsAetheryteUnlocked(skipConditions.AetheryteUnlocked.Value))
                    {
                        logger.LogInformation("Skipping aetheryte teleport due to SkipCondition (AetheryteUnlocked)");
                        return true;
                    }

                    if (ElementId != null)
                    {
                        QuestProgressInfo? questWork = questFunctions.GetQuestProgressInfo(ElementId);
                        if (skipConditions.RequiredQuestVariablesNotMet &&
                            questWork != null &&
                            !QuestWorkUtils.MatchesRequiredQuestWorkConfig(Step.RequiredQuestVariables, questWork,
                                logger))
                        {
                            logger.LogInformation("Skipping aetheryte teleport, as required variables do not match");
                            return true;
                        }
                    }



                    if (skipConditions.NearPosition is { } nearPosition && clientState.TerritoryType == Step.TerritoryId)
                    {
                        if (Vector3.Distance(nearPosition.Position, clientState.LocalPlayer!.Position) <= nearPosition.MaximumDistance)
                        {
                            logger.LogInformation("Skipping aetheryte shortcut, as we're near the position");
                            return true;
                        }
                    }
                }

                if (ExpectedTerritoryId == territoryType)
                {
                    if (!skipConditions.Never)
                    {
                        if (skipConditions is { InSameTerritory: true })
                        {
                            logger.LogInformation("Skipping aetheryte teleport due to SkipCondition (InSameTerritory)");
                            return true;
                        }

                        Vector3 pos = clientState.LocalPlayer!.Position;
                        if (Step.Position != null &&
                            (pos - Step.Position.Value).Length() < Step.CalculateActualStopDistance())
                        {
                            logger.LogInformation("Skipping aetheryte teleport, we're near the target");
                            return true;
                        }

                        if (aetheryteData.CalculateDistance(pos, territoryType, TargetAetheryte) < 20 ||
                            (Step.AethernetShortcut != null &&
                             (aetheryteData.CalculateDistance(pos, territoryType, Step.AethernetShortcut.From) < 20 ||
                              aetheryteData.CalculateDistance(pos, territoryType, Step.AethernetShortcut.To) < 20)))
                        {
                            logger.LogInformation("Skipping aetheryte teleport");
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool DoTeleport()
        {
            if (!aetheryteFunctions.CanTeleport(TargetAetheryte))
            {
                if (!aetheryteFunctions.IsTeleportUnlocked())
                    throw new TaskException("Teleport is not unlocked, attune to any aetheryte first.");

                _continueAt = DateTime.Now.AddSeconds(1);
                logger.LogTrace("Waiting for teleport cooldown...");
                return false;
            }

            _continueAt = DateTime.Now.AddSeconds(8);

            if (!aetheryteFunctions.IsAetheryteUnlocked(TargetAetheryte))
            {
                chatGui.PrintError($"[Questionable] Aetheryte {TargetAetheryte} is not unlocked.");
                throw new TaskException("Aetheryte is not unlocked");
            }
            else if (aetheryteFunctions.TeleportAetheryte(TargetAetheryte))
            {
                logger.LogInformation("Travelling via aetheryte...");
                return true;
            }
            else
            {
                chatGui.Print("[Questionable] Unable to teleport to aetheryte.");
                throw new TaskException("Unable to teleport to aetheryte");
            }
        }

        public override string ToString() => $"UseAetheryte({TargetAetheryte})";
    }
}
