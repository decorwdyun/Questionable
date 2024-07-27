using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps.Common;
using Questionable.Data;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Controller.Steps.Shared;

internal static class AetheryteShortcut
{
    internal sealed class Factory(
        IServiceProvider serviceProvider,
        GameFunctions gameFunctions,
        AetheryteData aetheryteData) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.AetheryteShortcut == null)
                return [];

            var task = serviceProvider.GetRequiredService<UseAetheryteShortcut>()
                .With(step, step.AetheryteShortcut.Value, aetheryteData.TerritoryIds[step.AetheryteShortcut.Value]);
            return
            [
                new WaitConditionTask(() => gameFunctions.CanTeleport(step.AetheryteShortcut.Value), "CanTeleport"),
                task
            ];
        }

        public ITask CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
            => throw new InvalidOperationException();
    }

    internal sealed class UseAetheryteShortcut(
        ILogger<UseAetheryteShortcut> logger,
        GameFunctions gameFunctions,
        IClientState clientState,
        IChatGui chatGui,
        AetheryteData aetheryteData) : ISkippableTask
    {
        private DateTime _continueAt;

        public QuestStep? Step { get; set; }
        public EAetheryteLocation TargetAetheryte { get; set; }

        /// <summary>
        /// If using an aethernet shortcut after, the aetheryte's territory-id and the step's territory-id can differ,
        /// we always use the aetheryte's territory-id.
        /// </summary>
        public ushort ExpectedTerritoryId { get; set; }

        public ITask With(QuestStep? step, EAetheryteLocation targetAetheryte, ushort expectedTerritoryId)
        {
            Step = step;
            TargetAetheryte = targetAetheryte;
            ExpectedTerritoryId = expectedTerritoryId;
            return this;
        }

        public bool Start()
        {
            _continueAt = DateTime.Now.AddSeconds(8);
            ushort territoryType = clientState.TerritoryType;
            if (Step != null && ExpectedTerritoryId == territoryType)
            {
                var skipConditions = Step.SkipConditions?.AetheryteShortcutIf ?? new();
                if (!skipConditions.Never)
                {
                    if (skipConditions is { InSameTerritory: true })
                    {
                        logger.LogInformation("Skipping aetheryte teleport due to SkipCondition");
                        return false;
                    }

                    Vector3 pos = clientState.LocalPlayer!.Position;
                    if (Step.Position != null &&
                        (pos - Step.Position.Value).Length() < Step.CalculateActualStopDistance())
                    {
                        logger.LogInformation("Skipping aetheryte teleport, we're near the target");
                        return false;
                    }

                    if (aetheryteData.CalculateDistance(pos, territoryType, TargetAetheryte) < 20 ||
                        (Step.AethernetShortcut != null &&
                         (aetheryteData.CalculateDistance(pos, territoryType, Step.AethernetShortcut.From) < 20 ||
                          aetheryteData.CalculateDistance(pos, territoryType, Step.AethernetShortcut.To) < 20)))
                    {
                        logger.LogInformation("Skipping aetheryte teleport");
                        return false;
                    }
                }
            }

            if (!gameFunctions.IsAetheryteUnlocked(TargetAetheryte))
            {
                chatGui.PrintError($"[Questionable] Aetheryte {TargetAetheryte} is not unlocked.");
                throw new TaskException("Aetheryte is not unlocked");
            }
            else if (gameFunctions.TeleportAetheryte(TargetAetheryte))
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

        public ETaskResult Update()
        {
            if (DateTime.Now >= _continueAt && clientState.TerritoryType == ExpectedTerritoryId)
                return ETaskResult.TaskComplete;

            return ETaskResult.StillRunning;
        }

        public override string ToString() => $"UseAetheryte({TargetAetheryte})";
    }
}
