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
    internal sealed class Factory(IServiceProvider serviceProvider) : SimpleTaskFactory
    {
        public override ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.Jump)
                return null;

            ArgumentNullException.ThrowIfNull(step.JumpDestination);

            if (step.JumpDestination.Type == EJumpType.SingleJump)
            {
                return serviceProvider.GetRequiredService<SingleJump>()
                    .With(step.DataId, step.JumpDestination, step.Comment);
            }
            else
            {
                return serviceProvider.GetRequiredService<RepeatedJumps>()
                    .With(step.DataId, step.JumpDestination, step.Comment);
            }
        }
    }

    internal class SingleJump(
        MovementController movementController,
        IClientState clientState,
        IFramework framework) : ITask
    {
        public uint? DataId { get; set; }
        public JumpDestination JumpDestination { get; set; } = null!;
        public string? Comment { get; set; }

        public ITask With(uint? dataId, JumpDestination jumpDestination, string? comment)
        {
            DataId = dataId;
            JumpDestination = jumpDestination;
            Comment = comment ?? string.Empty;
            return this;
        }

        public virtual bool Start()
        {
            float stopDistance = JumpDestination.CalculateStopDistance();
            if ((clientState.LocalPlayer!.Position - JumpDestination.Position).Length() <= stopDistance)
                return false;

            movementController.NavigateTo(EMovementType.Quest, DataId, [JumpDestination.Position], false, false,
                JumpDestination.StopDistance ?? stopDistance);
            framework.RunOnTick(() =>
                {
                    unsafe
                    {
                        ActionManager.Instance()->UseAction(ActionType.GeneralAction, 2);
                    }
                },
                TimeSpan.FromSeconds(JumpDestination.DelaySeconds ?? 0.5f));
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

        public override string ToString() => $"Jump({Comment})";
    }

    internal sealed class RepeatedJumps(
        MovementController movementController,
        IClientState clientState,
        IFramework framework,
        ILogger<RepeatedJumps> logger) : SingleJump(movementController, clientState, framework)
    {
        private readonly IClientState _clientState = clientState;
        private DateTime _continueAt = DateTime.MinValue;
        private int _attempts;

        public override bool Start()
        {
            _continueAt = DateTime.Now + TimeSpan.FromSeconds(2 * (JumpDestination.DelaySeconds ?? 0.5f));
            return base.Start();
        }

        public override ETaskResult Update()
        {
            if (DateTime.Now < _continueAt)
                return ETaskResult.StillRunning;

            float stopDistance = JumpDestination.CalculateStopDistance();
            if ((_clientState.LocalPlayer!.Position - JumpDestination.Position).Length() <= stopDistance ||
                _clientState.LocalPlayer.Position.Y >= JumpDestination.Position.Y - 0.5f)
                return ETaskResult.TaskComplete;

            logger.LogTrace("Y-Heights for jumps: player={A}, target={B}", _clientState.LocalPlayer.Position.Y,
                JumpDestination.Position.Y - 0.5f);
            unsafe
            {
                ActionManager.Instance()->UseAction(ActionType.GeneralAction, 2);
            }

            ++_attempts;
            if (_attempts >= 50)
                throw new TaskException("Tried to jump too many times, didn't reach the target");

            _continueAt = DateTime.Now + TimeSpan.FromSeconds(JumpDestination.DelaySeconds ?? 0.5f);
            return ETaskResult.StillRunning;
        }

        public override string ToString() => $"RepeatedJump({Comment})";
    }
}
