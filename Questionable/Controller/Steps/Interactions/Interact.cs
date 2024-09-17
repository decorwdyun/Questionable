using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps.Shared;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Interactions;

internal static class Interact
{
    internal sealed class Factory(
        GameFunctions gameFunctions,
        Configuration configuration,
        ICondition condition,
        ILoggerFactory loggerFactory)
        : ITaskFactory
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

            yield return Interact(step.DataId.Value, quest, step.InteractionType,
                step.TargetTerritoryId != null || quest.Id is SatisfactionSupplyNpcId ||
                step.SkipConditions is { StepIf.Never: true }, step.PickUpItemId, step.SkipConditions?.StepIf);
        }

        internal ITask Interact(uint dataId, Quest? quest, EInteractionType interactionType,
            bool skipMarkerCheck = false, uint? pickUpItemId = null, SkipStepConditions? skipConditions = null)
        {
            return new DoInteract(dataId, quest, interactionType, skipMarkerCheck, pickUpItemId, skipConditions,
                gameFunctions, condition, loggerFactory.CreateLogger<DoInteract>());
        }
    }

    internal sealed class DoInteract(
        uint dataId,
        Quest? quest,
        EInteractionType interactionType,
        bool skipMarkerCheck,
        uint? pickUpItemId,
        SkipStepConditions? skipConditions,
        GameFunctions gameFunctions,
        ICondition condition,
        ILogger<DoInteract> logger)
        : ITask
    {
        private bool _needsUnmount;
        private InteractionProgressContext? _progressContext;
        private DateTime _continueAt = DateTime.MinValue;

        public Quest? Quest => quest;

        public EInteractionType InteractionType
        {
            get => interactionType;
            set => interactionType = value;
        }

        public InteractionProgressContext? ProgressContext() => _progressContext;

        public bool Start()
        {
            IGameObject? gameObject = gameFunctions.FindObjectByDataId(dataId);
            if (gameObject == null)
            {
                logger.LogWarning("No game object with dataId {DataId}", dataId);
                return false;
            }

            if (!gameObject.IsTargetable && skipConditions is { Never: false, NotTargetable: true })
            {
                logger.LogInformation("Not interacting with {DataId} because it is not targetable (but skippable)",
                    dataId);
                return false;
            }

            // this is only relevant for followers on quests
            if (!gameObject.IsTargetable && condition[ConditionFlag.Mounted] &&
                gameObject.ObjectKind != ObjectKind.GatheringPoint)
            {
                logger.LogInformation("Preparing interaction for {DataId} by unmounting", dataId);
                _needsUnmount = true;
                gameFunctions.Unmount();
                _continueAt = DateTime.Now.AddSeconds(1);
                return true;
            }

            if (gameObject.IsTargetable && HasAnyMarker(gameObject))
            {
                _progressContext =
                    InteractionProgressContext.FromActionUseOrDefault(() => gameFunctions.InteractWith(gameObject));
                _continueAt = DateTime.Now.AddSeconds(0.5);
                return true;
            }

            return true;
        }

        public ETaskResult Update()
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

            if (pickUpItemId != null)
            {
                unsafe
                {
                    InventoryManager* inventoryManager = InventoryManager.Instance();
                    if (inventoryManager->GetInventoryItemCount(pickUpItemId.Value) > 0)
                        return ETaskResult.TaskComplete;
                }
            }
            else
            {
                if (_progressContext != null && _progressContext.WasSuccessful())
                    return ETaskResult.TaskComplete;

                if (interactionType == EInteractionType.Gather && condition[ConditionFlag.Gathering])
                    return ETaskResult.TaskComplete;
            }

            IGameObject? gameObject = gameFunctions.FindObjectByDataId(dataId);
            if (gameObject == null || !gameObject.IsTargetable || !HasAnyMarker(gameObject))
                return ETaskResult.StillRunning;

            _progressContext =
                InteractionProgressContext.FromActionUseOrDefault(() => gameFunctions.InteractWith(gameObject));
            _continueAt = DateTime.Now.AddSeconds(0.5);
            return ETaskResult.StillRunning;
        }

        private unsafe bool HasAnyMarker(IGameObject gameObject)
        {
            if (skipMarkerCheck || gameObject.ObjectKind != ObjectKind.EventNpc)
                return true;

            var gameObjectStruct = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)gameObject.Address;
            return gameObjectStruct->NamePlateIconId != 0;
        }

        public override string ToString() => $"Interact({dataId})";
    }
}
