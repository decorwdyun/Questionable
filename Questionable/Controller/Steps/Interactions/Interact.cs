using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps.Shared;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Interactions;

internal static class Interact
{
    internal sealed class Factory(GameFunctions gameFunctions, ICondition condition, ILoggerFactory loggerFactory) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType is EInteractionType.AcceptQuest or EInteractionType.CompleteQuest
                or EInteractionType.AcceptLeve or EInteractionType.CompleteLeve
                or EInteractionType.SinglePlayerDuty)
            {
                if (step.Emote != null)
                    yield break;

                if (step.DataId == null)
                    yield break;
            }
            else if (step.InteractionType != EInteractionType.Interact)
                yield break;

            ArgumentNullException.ThrowIfNull(step.DataId);

            // if we're fast enough, it is possible to get the smalltalk prompt
            if (sequence.Sequence == 0 && sequence.Steps.IndexOf(step) == 0)
                yield return new WaitAtEnd.WaitDelay();

            yield return Interact(step.DataId.Value, quest, step.InteractionType,
                    step.TargetTerritoryId != null || quest.Id is SatisfactionSupplyNpcId);
        }

        internal ITask Interact(uint dataId, Quest? quest, EInteractionType interactionType, bool skipMarkerCheck = false)
        {
            return new DoInteract(dataId, quest, interactionType, skipMarkerCheck, gameFunctions, condition,
                loggerFactory.CreateLogger<DoInteract>());
        }
    }

    internal sealed class DoInteract(
        uint dataId,
        Quest? quest,
        EInteractionType interactionType,
        bool skipMarkerCheck,
        GameFunctions gameFunctions,
        ICondition condition,
        ILogger<DoInteract> logger)
        : ITask, IConditionChangeAware
    {
        private bool _needsUnmount;
        private EInteractionState _interactionState = EInteractionState.None;
        private DateTime _continueAt = DateTime.MinValue;

        public Quest? Quest => quest;
        public EInteractionType InteractionType
        {
            get => interactionType;
            set => interactionType = value;
        }

        public bool Start()
        {
            IGameObject? gameObject = gameFunctions.FindObjectByDataId(dataId);
            if (gameObject == null)
            {
                logger.LogWarning("No game object with dataId {DataId}", dataId);
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
                _interactionState = gameFunctions.InteractWith(gameObject)
                    ? EInteractionState.InteractionTriggered
                    : EInteractionState.None;
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

            if (_interactionState == EInteractionState.InteractionConfirmed)
                return ETaskResult.TaskComplete;

            if (interactionType == EInteractionType.InternalGather && condition[ConditionFlag.Gathering])
                return ETaskResult.TaskComplete;

            IGameObject? gameObject = gameFunctions.FindObjectByDataId(dataId);
            if (gameObject == null || !gameObject.IsTargetable || !HasAnyMarker(gameObject))
                return ETaskResult.StillRunning;

            _interactionState = gameFunctions.InteractWith(gameObject)
                ? EInteractionState.InteractionTriggered
                : EInteractionState.None;
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

        public void OnConditionChange(ConditionFlag flag, bool value)
        {
            logger.LogDebug("Condition change: {Flag} = {Value}", flag, value);
            if (_interactionState == EInteractionState.InteractionTriggered &&
                flag == ConditionFlag.OccupiedInQuestEvent && value)
            {
                logger.LogInformation("Interaction was most likely triggered");
                _interactionState = EInteractionState.InteractionConfirmed;
            }
        }

        private enum EInteractionState
        {
            None,
            InteractionTriggered,
            InteractionConfirmed,
        }
    }
}
