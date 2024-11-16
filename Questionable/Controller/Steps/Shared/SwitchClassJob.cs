﻿using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using LLib.GameData;
using Questionable.Controller.Steps.Common;
using Questionable.Data;
using Questionable.Model;
using Questionable.Model.Questing;

namespace Questionable.Controller.Steps.Shared;

internal static class SwitchClassJob
{
    internal sealed class Factory : SimpleTaskFactory
    {
        public override ITask? CreateTask(Quest quest, QuestSequence sequence, QuestStep step)
        {
            if (step.InteractionType != EInteractionType.SwitchClass)
                return null;

            EClassJob classJob = ClassJobUtils.AsIndividualJobs(step.TargetClass).Single();
            return new Task(classJob);
        }
    }
    internal sealed record Task(EClassJob ClassJob) : ITask
    {
        public override string ToString() => $"SwitchJob({ClassJob})";
    }

    internal sealed class SwitchClassJobExecutor(IClientState clientState) : AbstractDelayedTaskExecutor<Task>
    {
        protected override unsafe bool StartInternal()
        {
            if (clientState.LocalPlayer!.ClassJob.RowId == (uint)Task.ClassJob)
                return false;

            var gearsetModule = RaptureGearsetModule.Instance();
            if (gearsetModule != null)
            {
                for (int i = 0; i < 100; ++i)
                {
                    var gearset = gearsetModule->GetGearset(i);
                    if (gearset->ClassJob == (byte)Task.ClassJob)
                    {
                        gearsetModule->EquipGearset(gearset->Id);
                        return true;
                    }
                }
            }

            throw new TaskException($"No gearset found for {Task.ClassJob}");
        }

        protected override ETaskResult UpdateInternal() => ETaskResult.TaskComplete;
    }
}
