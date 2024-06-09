using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Controller.Steps.InteractionFactory;

internal static class Interact
{
    internal sealed class Factory(IServiceProvider serviceProvider) : ITaskFactory
    {
        public ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.Interact)
                return null;

            ArgumentNullException.ThrowIfNull(step.DataId);

            return serviceProvider.GetRequiredService<DoInteract>().With(step.DataId.Value);
        }
    }

    internal sealed class DoInteract(GameFunctions gameFunctions, ICondition condition, ILogger<DoInteract> logger)
        : ITask
    {
        private bool _interacted;
        private DateTime _continueAt = DateTime.MinValue;

        private uint DataId { get; set; }

        public ITask With(uint dataId)
        {
            DataId = dataId;
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
                gameFunctions.Unmount();
                _continueAt = DateTime.Now.AddSeconds(0.5);
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
            if (gameObject.ObjectKind != ObjectKind.EventNpc)
                return true;

            var gameObjectStruct = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)gameObject.Address;
            return gameObjectStruct->NamePlateIconId != 0;
        }

        public override string ToString() => $"Interact({DataId})";
    }
}
