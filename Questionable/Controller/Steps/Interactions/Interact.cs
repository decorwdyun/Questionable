using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps.Shared;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Interactions;

internal static class Interact
{
    internal sealed class Factory(Configuration configuration) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType is EInteractionType.AcceptQuest or EInteractionType.CompleteQuest
                or EInteractionType.AcceptLeve or EInteractionType.CompleteLeve
                or EInteractionType.SinglePlayerDuty)
            {
                if (step.Emote != null)
                    yield break;

                if (step.ChatMessage != null)
                    yield break;

                if (step.ItemId != null)
                    yield break;

                if (step.DataId == null)
                    yield break;
            }
            else if (step.InteractionType == EInteractionType.Snipe)
            {
                if (!configuration.General.AutomaticallyCompleteSnipeTasks)
                    yield break;
            }
            else if (step.InteractionType != EInteractionType.Interact)
                yield break;

            ArgumentNullException.ThrowIfNull(step.DataId);

            // if we're fast enough, it is possible to get the smalltalk prompt
            if (sequence.Sequence == 0 && sequence.Steps.IndexOf(step) == 0)
                yield return new WaitAtEnd.WaitDelay();

            yield return new Task(step.DataId.Value, quest, step.InteractionType,
                step.TargetTerritoryId != null || quest.Id is SatisfactionSupplyNpcId ||
                step.SkipConditions is { StepIf.Never: true }, step.PickUpItemId, step.SkipConditions?.StepIf);
        }
    }

    internal sealed record Task(
        uint DataId,
        Quest? Quest,
        EInteractionType InteractionType,
        bool SkipMarkerCheck = false,
        uint? PickUpItemId = null,
        SkipStepConditions? SkipConditions = null) : ITask
    {
        public bool ShouldRedoOnInterrupt() => true;

        public override string ToString() => $"Interact({DataId})";
    }

    internal sealed class DoInteract(
        GameFunctions gameFunctions,
        ICondition condition,
        ILogger<DoInteract> logger)
        : TaskExecutor<Task>
    {
        private bool _needsUnmount;
        private DateTime _continueAt = DateTime.MinValue;

        public Quest? Quest => Task.Quest;
        public EInteractionType InteractionType { get; set; }

        protected override bool Start()
        {
            InteractionType = Task.InteractionType;

            IGameObject? gameObject = gameFunctions.FindObjectByDataId(Task.DataId);
            if (gameObject == null)
            {
                logger.LogWarning("No game object with dataId {DataId}", Task.DataId);
                return false;
            }

            if (!gameObject.IsTargetable && Task.SkipConditions is { Never: false, NotTargetable: true })
            {
                logger.LogInformation("Not interacting with {DataId} because it is not targetable (but skippable)",
                    Task.DataId);
                return false;
            }

            // this is only relevant for followers on quests
            if (!gameObject.IsTargetable && condition[ConditionFlag.Mounted] &&
                gameObject.ObjectKind != ObjectKind.GatheringPoint)
            {
                logger.LogInformation("Preparing interaction for {DataId} by unmounting", Task.DataId);
                _needsUnmount = true;
                gameFunctions.Unmount();
                _continueAt = DateTime.Now.AddSeconds(1);
                return true;
            }

            if (gameObject.IsTargetable && HasAnyMarker(gameObject))
            {
                ProgressContext =
                    InteractionProgressContext.FromActionUseOrDefault(() => gameFunctions.InteractWith(gameObject));
                _continueAt = DateTime.Now.AddSeconds(0.5);
                return true;
            }

            return true;
        }

        public override ETaskResult Update()
        {
            if (DateTime.Now <= _continueAt)
                return ETaskResult.StillRunning;

            if (_needsUnmount)
            {
                if (condition[ConditionFlag.Mounted])
                {
                    gameFunctions.Unmount();
                    _continueAt = DateTime.Now.AddSeconds(1);
                    return ETaskResult.StillRunning;
                }
                else
                    _needsUnmount = false;
            }

            if (Task.PickUpItemId != null)
            {
                unsafe
                {
                    InventoryManager* inventoryManager = InventoryManager.Instance();
                    if (inventoryManager->GetInventoryItemCount(Task.PickUpItemId.Value) > 0)
                        return ETaskResult.TaskComplete;
                }
            }
            else
            {
                if (ProgressContext != null && ProgressContext.WasSuccessful())
                    return ETaskResult.TaskComplete;

                if (InteractionType == EInteractionType.Gather && condition[ConditionFlag.Gathering])
                    return ETaskResult.TaskComplete;
            }

            IGameObject? gameObject = gameFunctions.FindObjectByDataId(Task.DataId);
            if (gameObject == null || !gameObject.IsTargetable || !HasAnyMarker(gameObject))
                return ETaskResult.StillRunning;

            ProgressContext =
                InteractionProgressContext.FromActionUseOrDefault(() => gameFunctions.InteractWith(gameObject));
            _continueAt = DateTime.Now.AddSeconds(0.5);
            return ETaskResult.StillRunning;
        }

        private unsafe bool HasAnyMarker(IGameObject gameObject)
        {
            if (Task.SkipMarkerCheck || gameObject.ObjectKind != ObjectKind.EventNpc)
                return true;

            var gameObjectStruct = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)gameObject.Address;
            return gameObjectStruct->NamePlateIconId != 0;
        }
    }
}
