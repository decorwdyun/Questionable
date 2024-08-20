using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps.Common;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Interactions;

internal static class Action
{
    internal sealed class Factory(GameFunctions gameFunctions, Mount.Factory mountFactory, ILoggerFactory loggerFactory)
        : ITaskFactory
    {
        public IEnumerable<ITask> CreateAllTasks(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.Action)
                return [];

            ArgumentNullException.ThrowIfNull(step.Action);

            var task = new UseOnObject(step.DataId, step.Action.Value, gameFunctions,
                loggerFactory.CreateLogger<UseOnObject>());
            if (step.Action.Value.RequiresMount())
                return [task];
            else
                return [mountFactory.Unmount(), task];
        }
    }

    private sealed class UseOnObject(
        uint? dataId,
        EAction action,
        GameFunctions gameFunctions,
        ILogger<UseOnObject> logger) : ITask
    {
        private bool _usedAction;
        private DateTime _continueAt = DateTime.MinValue;

        public bool Start()
        {
            if (dataId != null)
            {
                IGameObject? gameObject = gameFunctions.FindObjectByDataId(dataId.Value);
                if (gameObject == null)
                {
                    logger.LogWarning("No game object with dataId {DataId}", dataId);
                    return false;
                }

                if (gameObject.IsTargetable)
                {
                    _usedAction = gameFunctions.UseAction(gameObject, action);
                    _continueAt = DateTime.Now.AddSeconds(0.5);
                    return true;
                }
            }
            else
            {
                _usedAction = gameFunctions.UseAction(action);
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
                if (dataId != null)
                {
                    IGameObject? gameObject = gameFunctions.FindObjectByDataId(dataId.Value);
                    if (gameObject == null || !gameObject.IsTargetable)
                        return ETaskResult.StillRunning;

                    _usedAction = gameFunctions.UseAction(gameObject, action);
                    _continueAt = DateTime.Now.AddSeconds(0.5);
                }
                else
                {
                    _usedAction = gameFunctions.UseAction(action);
                    _continueAt = DateTime.Now.AddSeconds(0.5);
                }

                return ETaskResult.StillRunning;
            }

            return ETaskResult.TaskComplete;
        }

        public override string ToString() => $"Action({action})";
    }
}
