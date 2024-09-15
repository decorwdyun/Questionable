using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
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

            var task = OnObject(step.DataId, step.Action.Value);
            if (step.Action.Value.RequiresMount())
                return [task];
            else
                return [mountFactory.Unmount(), task];
        }

        public ITask OnObject(uint? dataId, EAction action)
        {
            return new UseOnObject(dataId, action, gameFunctions,
                loggerFactory.CreateLogger<UseOnObject>());
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
                    if (action == EAction.Diagnosis)
                    {
                        uint eukrasiaAura = 2606;
                        // If SGE have Eukrasia status, we need to remove it.
                        if (gameFunctions.HasStatus(eukrasiaAura))
                        {
                            if (GameFunctions.RemoveStatus(eukrasiaAura))
                            {
                                // Introduce a delay of 2 seconds before using the next action (otherwise it will try and use Eukrasia Diagnosis)
                                _continueAt = DateTime.Now.AddSeconds(2);
                                return true; 
                            }
                        }
                    }
                    
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