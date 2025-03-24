using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps.Common;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Interactions;

internal static class Action
{
    internal sealed class Factory : ITaskFactory
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
                return [new Mount.UnmountTask(), task];
        }

        public static ITask OnObject(uint? dataId, EAction action)
        {
            if (action is EAction.FumaShuriken or EAction.Katon or EAction.Raiton)
            {
                ArgumentNullException.ThrowIfNull(dataId);
                return new UseMudraOnObject(dataId.Value, action);
            }
            else
                return new UseOnObject(dataId, action);
        }
    }

    internal sealed record UseOnObject(
        uint? DataId,
        EAction Action) : ITask
    {
        public override string ToString() => $"技能({Action})";
    }

    internal sealed class UseOnObjectExecutor(
        GameFunctions gameFunctions,
        ILogger<UseOnObject> logger) : TaskExecutor<UseOnObject>
    {
        private bool _usedAction;
        private DateTime _continueAt = DateTime.MinValue;

        protected override bool Start()
        {
            if (Task.DataId != null)
            {
                IGameObject? gameObject = gameFunctions.FindObjectByDataId(Task.DataId.Value);
                if (gameObject == null)
                {
                    logger.LogWarning("No game object with dataId {DataId}", Task.DataId);
                    return false;
                }

                if (gameObject.IsTargetable)
                {
                    if (Task.Action == EAction.Diagnosis)
                    {
                        // If SGE have Eukrasia status, we need to remove it.
                        if (gameFunctions.HasStatus(EStatus.Eukrasia))
                        {
                            if (GameFunctions.RemoveStatus(EStatus.Eukrasia))
                            {
                                // Introduce a delay of 2 seconds before using the next action (otherwise it will try and use Eukrasia Diagnosis)
                                _continueAt = DateTime.Now.AddSeconds(2);
                                return true;
                            }
                        }
                    }

                    _usedAction = gameFunctions.UseAction(gameObject, Task.Action);
                    _continueAt = DateTime.Now.AddSeconds(0.5);
                    return true;
                }
            }
            else
            {
                _usedAction = gameFunctions.UseAction(Task.Action);
                _continueAt = DateTime.Now.AddSeconds(0.5);
                return true;
            }

            return true;
        }

        public override ETaskResult Update()
        {
            if (DateTime.Now <= _continueAt)
                return ETaskResult.StillRunning;

            if (!_usedAction)
            {
                if (Task.DataId != null)
                {
                    IGameObject? gameObject = gameFunctions.FindObjectByDataId(Task.DataId.Value);
                    if (gameObject == null || !gameObject.IsTargetable)
                        return ETaskResult.StillRunning;

                    _usedAction = gameFunctions.UseAction(gameObject, Task.Action);
                    _continueAt = DateTime.Now.AddSeconds(0.5);
                }
                else
                {
                    _usedAction = gameFunctions.UseAction(Task.Action);
                    _continueAt = DateTime.Now.AddSeconds(0.5);
                }

                return ETaskResult.StillRunning;
            }

            return ETaskResult.TaskComplete;
        }

        public override bool ShouldInterruptOnDamage() => false;
    }

    internal sealed record UseMudraOnObject(uint DataId, EAction Action) : ITask
    {
        public override string ToString() => $"Mudra({Action})";
    }

    internal sealed class UseMudraOnObjectExecutor(
        GameFunctions gameFunctions,
        ILogger<UseMudraOnObject> logger) : TaskExecutor<UseMudraOnObject>
    {
        private static readonly ReadOnlyDictionary<EAction, Dictionary<EAction, EAction>> Combos =
            new Dictionary<EAction, Dictionary<EAction, EAction>>
            {
                { EAction.FumaShuriken, new() { { EAction.Ninjutsu, EAction.Ten } } },
                { EAction.Raiton, new() { { EAction.Ninjutsu, EAction.Ten }, { EAction.FumaShuriken, EAction.Chi } } },
                { EAction.Katon, new() {{ EAction.Ninjutsu, EAction.Chi }, { EAction.FumaShuriken, EAction.Ten } } }
            }.AsReadOnly();

        private DateTime _continueAt = DateTime.MinValue;

        protected override bool Start() => true;

        public override unsafe ETaskResult Update()
        {
            if (DateTime.Now < _continueAt)
                return ETaskResult.StillRunning;

            EAction adjustedNinjutsuId = (EAction)ActionManager.Instance()->GetAdjustedActionId((uint)EAction.Ninjutsu);
            if (adjustedNinjutsuId == EAction.RabbitMedium)
            {
                _continueAt = DateTime.Now.AddSeconds(1);
                return ETaskResult.StillRunning;
            }

            IGameObject? gameObject = gameFunctions.FindObjectByDataId(Task.DataId);
            if (gameObject == null || !gameObject.IsTargetable)
                return ETaskResult.StillRunning;

            if (adjustedNinjutsuId == Task.Action)
            {
                _continueAt = DateTime.Now.AddSeconds(0.25);
                return gameFunctions.UseAction(gameObject, Task.Action)
                    ? ETaskResult.TaskComplete
                    : ETaskResult.StillRunning;
            }

            if (Combos.TryGetValue(Task.Action, out var combo))
            {
                if (combo.TryGetValue(adjustedNinjutsuId, out var mudra))
                {
                    _continueAt = DateTime.Now.AddSeconds(0.25);
                    gameFunctions.UseAction(mudra);
                    return ETaskResult.StillRunning;
                }

                _continueAt = DateTime.Now.AddSeconds(0.25);
                return ETaskResult.StillRunning;
            }

            logger.LogError("Unable to find relevant combo for {Action}", Task.Action);
            return ETaskResult.TaskComplete;
        }

        public override bool ShouldInterruptOnDamage() => false;
    }
}
