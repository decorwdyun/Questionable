using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Microsoft.Extensions.DependencyInjection;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Interactions;

internal static class EquipRecommended
{
    internal sealed class Factory(IServiceProvider serviceProvider) : ITaskFactory
    {
        public ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.EquipRecommended)
                return null;

            return serviceProvider.GetRequiredService<DoEquipRecommended>();
        }
    }

    internal sealed unsafe class DoEquipRecommended(IClientState clientState) : ITask
    {
        private bool _equipped;

        public bool Start()
        {
            RecommendEquipModule.Instance()->SetupForClassJob((byte)clientState.LocalPlayer!.ClassJob.Id);
            return true;
        }

        public ETaskResult Update()
        {
            if (RecommendEquipModule.Instance()->IsUpdating)
                return ETaskResult.StillRunning;

            if (!_equipped)
            {
                RecommendEquipModule.Instance()->EquipRecommendedGear();
                _equipped = true;
                return ETaskResult.StillRunning;
            }

            return ETaskResult.TaskComplete;
        }

        public override string ToString() => "EquipRecommended";
    }
}
