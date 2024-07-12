using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Microsoft.Extensions.DependencyInjection;
using Questionable.Controller.Steps.Common;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Controller.Steps.Interactions;

internal static class Jump
{
    internal sealed class Factory(IServiceProvider serviceProvider) : ITaskFactory
    {
        public ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.Jump)
                return null;

            ArgumentNullException.ThrowIfNull(step.JumpDestination);

            return serviceProvider.GetRequiredService<DoJump>()
                .With(step.DataId, step.JumpDestination, step.Comment);
        }
    }

    internal sealed class DoJump(
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

        public bool Start()
        {
            float stopDistance = JumpDestination.StopDistance ?? 1f;
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

        public ETaskResult Update()
        {
            if (movementController.IsPathfinding || movementController.IsPathRunning)
                return ETaskResult.StillRunning;

            DateTime movementStartedAt = movementController.MovementStartedAt;
            if (movementStartedAt == DateTime.MaxValue || movementStartedAt.AddSeconds(2) >= DateTime.Now)
                return ETaskResult.StillRunning;

            return ETaskResult.TaskComplete;
        }

        public override string ToString() => $"Jump({Comment})";
    }
}
