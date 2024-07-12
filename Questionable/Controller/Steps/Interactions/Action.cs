using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps.Common;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Controller.Steps.Interactions;

internal static class Action
{
    internal sealed class Factory(IServiceProvider serviceProvider) : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.Action)
                return [];

            ArgumentNullException.ThrowIfNull(step.Action);

            var task = serviceProvider.GetRequiredService<UseOnObject>()
                .With(step.DataId, step.Action.Value);
            if (step.Action.Value.RequiresMount())
                return [task];
            else
            {
                var unmount = serviceProvider.GetRequiredService<UnmountTask>();
                return [unmount, task];
            }
        }

        public ITask CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
            => throw new InvalidOperationException();
    }

    internal sealed class UseOnObject(GameFunctions gameFunctions, ILogger<UseOnObject> logger) : ITask
    {
        private bool _usedAction;
        private DateTime _continueAt = DateTime.MinValue;

        public uint? DataId { get; set; }
        public EAction Action { get; set; }

        public ITask With(uint? dataId, EAction action)
        {
            DataId = dataId;
            Action = action;
            return this;
        }

        public bool Start()
        {
            if (DataId != null)
            {
                IGameObject? gameObject = gameFunctions.FindObjectByDataId(DataId.Value);
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
            }
            else
            {
                _usedAction = gameFunctions.UseAction(Action);
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
                if (DataId != null)
                {
                    IGameObject? gameObject = gameFunctions.FindObjectByDataId(DataId.Value);
                    if (gameObject == null || !gameObject.IsTargetable)
                        return ETaskResult.StillRunning;

                    _usedAction = gameFunctions.UseAction(gameObject, Action);
                    _continueAt = DateTime.Now.AddSeconds(0.5);
                }
                else
                {
                    _usedAction = gameFunctions.UseAction(Action);
                    _continueAt = DateTime.Now.AddSeconds(0.5);
                }

                return ETaskResult.StillRunning;
            }

            return ETaskResult.TaskComplete;
        }

        public override string ToString() => $"Action({Action})";
    }
}
