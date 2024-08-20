using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Questionable.Controller.Steps.Common;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Interactions;

internal static class Jump
{
    internal sealed class Factory(
        MovementController movementController,
        IClientState clientState,
        IFramework framework,
        ILoggerFactory loggerFactory) : SimpleTaskFactory
    {
        public override ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.Jump)
                return null;

            ArgumentNullException.ThrowIfNull(step.JumpDestination);

            if (step.JumpDestination.Type == EJumpType.SingleJump)
                return SingleJump(step.DataId, step.JumpDestination, step.Comment);
            else
                return RepeatedJumps(step.DataId, step.JumpDestination, step.Comment);
        }

        public ITask SingleJump(uint? dataId, JumpDestination jumpDestination, string? comment)
        {
            return new DoSingleJump(dataId, jumpDestination, comment, movementController, clientState, framework);
        }

        public ITask RepeatedJumps(uint? dataId, JumpDestination jumpDestination, string? comment)
        {
            return new DoRepeatedJumps(dataId, jumpDestination, comment, movementController, clientState, framework,
                loggerFactory.CreateLogger<DoRepeatedJumps>());
        }
    }

    private class DoSingleJump(
        uint? dataId,
        JumpDestination jumpDestination,
        string? comment,
        MovementController movementController,
        IClientState clientState,
        IFramework framework) : ITask
    {
        public virtual bool Start()
        {
            float stopDistance = jumpDestination.CalculateStopDistance();
            if ((clientState.LocalPlayer!.Position - jumpDestination.Position).Length() <= stopDistance)
                return false;

            movementController.NavigateTo(EMovementType.Quest, dataId, [jumpDestination.Position], false, false,
                jumpDestination.StopDistance ?? stopDistance);
            framework.RunOnTick(() =>
                {
                    unsafe
                    {
                        ActionManager.Instance()->UseAction(ActionType.GeneralAction, 2);
                    }
                },
                TimeSpan.FromSeconds(jumpDestination.DelaySeconds ?? 0.5f));
            return true;
        }

        public virtual ETaskResult Update()
        {
            if (movementController.IsPathfinding || movementController.IsPathRunning)
                return ETaskResult.StillRunning;

            DateTime movementStartedAt = movementController.MovementStartedAt;
            if (movementStartedAt == DateTime.MaxValue || movementStartedAt.AddSeconds(1) >= DateTime.Now)
                return ETaskResult.StillRunning;

            return ETaskResult.TaskComplete;
        }

        public override string ToString() => $"Jump({comment})";
    }

    private sealed class DoRepeatedJumps(
        uint? dataId,
        JumpDestination jumpDestination,
        string? comment,
        MovementController movementController,
        IClientState clientState,
        IFramework framework,
        ILogger<DoRepeatedJumps> logger)
        : DoSingleJump(dataId, jumpDestination, comment, movementController, clientState, framework)
    {
        private readonly JumpDestination _jumpDestination = jumpDestination;
        private readonly string? _comment = comment;
        private readonly IClientState _clientState = clientState;
        private DateTime _continueAt = DateTime.MinValue;
        private int _attempts;

        public override bool Start()
        {
            _continueAt = DateTime.Now + TimeSpan.FromSeconds(2 * (_jumpDestination.DelaySeconds ?? 0.5f));
            return base.Start();
        }

        public override ETaskResult Update()
        {
            if (DateTime.Now < _continueAt)
                return ETaskResult.StillRunning;

            float stopDistance = _jumpDestination.CalculateStopDistance();
            if ((_clientState.LocalPlayer!.Position - _jumpDestination.Position).Length() <= stopDistance ||
                _clientState.LocalPlayer.Position.Y >= _jumpDestination.Position.Y - 0.5f)
                return ETaskResult.TaskComplete;

            logger.LogTrace("Y-Heights for jumps: player={A}, target={B}", _clientState.LocalPlayer.Position.Y,
                _jumpDestination.Position.Y - 0.5f);
            unsafe
            {
                ActionManager.Instance()->UseAction(ActionType.GeneralAction, 2);
            }

            ++_attempts;
            if (_attempts >= 50)
                throw new TaskException("Tried to jump too many times, didn't reach the target");

            _continueAt = DateTime.Now + TimeSpan.FromSeconds(_jumpDestination.DelaySeconds ?? 0.5f);
            return ETaskResult.StillRunning;
        }

        public override string ToString() => $"RepeatedJump({_comment})";
    }
}
