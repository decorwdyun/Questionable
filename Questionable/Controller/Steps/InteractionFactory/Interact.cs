using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps.BaseFactory;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Controller.Steps.InteractionFactory;

internal static class Interact
{
    internal sealed class Factory(IServiceProvider serviceProvider) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.Interact)
                yield break;

            ArgumentNullException.ThrowIfNull(step.DataId);

            // if we're fast enough, it is possible to get the smalltalk prompt
            if (sequence.Sequence == 0 && sequence.Steps.IndexOf(step) == 0)
                yield return serviceProvider.GetRequiredService<WaitAtEnd.WaitDelay>();

            yield return serviceProvider.GetRequiredService<DoInteract>()
                .With(step.DataId.Value, step.TargetTerritoryId != null);
        }

        public ITask CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
            => throw new InvalidOperationException();
    }

    internal sealed class DoInteract(GameFunctions gameFunctions, ICondition condition, ILogger<DoInteract> logger)
        : ITask
    {
        private bool _needsUnmount;
        private bool _interacted;
        private DateTime _continueAt = DateTime.MinValue;

        private uint DataId { get; set; }
        private bool SkipMarkerCheck { get; set; }

        public ITask With(uint dataId, bool skipMarkerCheck)
        {
            DataId = dataId;
            SkipMarkerCheck = skipMarkerCheck;
            return this;
        }

        public bool Start()
        {
            GameObject? gameObject = gameFunctions.FindObjectByDataId(DataId);
            if (gameObject == null)
            {
                logger.LogWarning("No game object with dataId {DataId}", DataId);
                return false;
            }

            // this is only relevant for followers on quests
            if (!gameObject.IsTargetable && condition[ConditionFlag.Mounted])
            {
                _needsUnmount = true;
                gameFunctions.Unmount();
                _continueAt = DateTime.Now.AddSeconds(1);
                return true;
            }

            if (gameObject.IsTargetable && HasAnyMarker(gameObject))
            {
                _interacted = gameFunctions.InteractWith(DataId);
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

            if (!_interacted)
            {
                GameObject? gameObject = gameFunctions.FindObjectByDataId(DataId);
                if (gameObject == null || !gameObject.IsTargetable || !HasAnyMarker(gameObject))
                    return ETaskResult.StillRunning;

                _interacted = gameFunctions.InteractWith(DataId);
                _continueAt = DateTime.Now.AddSeconds(0.5);
                return ETaskResult.StillRunning;
            }

            return ETaskResult.TaskComplete;
        }

        private unsafe bool HasAnyMarker(GameObject gameObject)
        {
            if (SkipMarkerCheck || gameObject.ObjectKind != ObjectKind.EventNpc)
                return true;

            var gameObjectStruct = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)gameObject.Address;
            return gameObjectStruct->NamePlateIconId != 0;
        }

        public override string ToString() => $"Interact({DataId})";
    }
}
