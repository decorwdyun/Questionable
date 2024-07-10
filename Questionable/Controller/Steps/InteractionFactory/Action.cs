using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps.BaseTasks;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Controller.Steps.InteractionFactory;

internal static class Action
{
    internal sealed class Factory(IServiceProvider serviceProvider) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.Action)
                return [];

            ArgumentNullException.ThrowIfNull(step.DataId);
            ArgumentNullException.ThrowIfNull(step.Action);

            var unmount = serviceProvider.GetRequiredService<UnmountTask>();
            var task = serviceProvider.GetRequiredService<UseOnObject>()
                .With(step.DataId.Value, step.Action.Value);
            return [unmount, task];
        }

        public ITask CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
            => throw new InvalidOperationException();
    }

    internal sealed class UseOnObject(GameFunctions gameFunctions, ILogger<UseOnObject> logger) : ITask
    {
        private bool _usedAction;
        private DateTime _continueAt = DateTime.MinValue;

        public uint DataId { get; set; }
        public EAction Action { get; set; }

        public ITask With(uint dataId, EAction action)
        {
            DataId = dataId;
            Action = action;
            return this;
        }

        public bool Start()
        {
            IGameObject? gameObject = gameFunctions.FindObjectByDataId(DataId);
            if (gameObject == null)
            {
                logger.LogWarning("No game object with dataId {DataId}", DataId);
                return false;
            }

            if (gameObject.IsTargetable)
            {
                _usedAction = gameFunctions.UseAction(gameObject, Action);
                _continueAt = DateTime.Now.AddSeconds(0.5);
                return true;
            }

            return true;
        }

        public ETaskResult Update()
        {
            if (DateTime.Now <= _continueAt)
                return ETaskResult.StillRunning;

            if (!_usedAction)
            {
                IGameObject? gameObject = gameFunctions.FindObjectByDataId(DataId);
                if (gameObject == null || !gameObject.IsTargetable)
                    return ETaskResult.StillRunning;

                _usedAction = gameFunctions.UseAction(gameObject, Action);
                _continueAt = DateTime.Now.AddSeconds(0.5);
                return ETaskResult.StillRunning;
            }

            return ETaskResult.TaskComplete;
        }

        public override string ToString() => $"Action({Action})";
    }
}
