using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using LLib.GameData;
using Questionable.Controller.Steps.Common;

namespace Questionable.Controller.Steps.Shared;

internal sealed class SwitchClassJob(EClassJob classJob, IClientState clientState) : AbstractDelayedTask
{
    protected override unsafe bool StartInternal()
    {
        if (clientState.LocalPlayer!.ClassJob.Id == (uint)classJob)
            return false;

        var gearsetModule = RaptureGearsetModule.Instance();
        if (gearsetModule != null)
        {
            for (int i = 0; i < 100; ++i)
            {
                var gearset = gearsetModule->GetGearset(i);
                if (gearset->ClassJob == (byte)classJob)
                {
                    gearsetModule->EquipGearset(gearset->Id);
                    return true;
                }
            }
        }

        throw new TaskException($"No gearset found for {classJob}");
    }

    protected override ETaskResult UpdateInternal() => ETaskResult.TaskComplete;

    public override string ToString() => $"SwitchJob({classJob})";
}
