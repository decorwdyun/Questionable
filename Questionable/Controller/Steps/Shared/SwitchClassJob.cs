using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using LLib.GameData;
using Questionable.Controller.Steps.Common;

namespace Questionable.Controller.Steps.Shared;

internal sealed class SwitchClassJob(IClientState clientState) : AbstractDelayedTask
{
    private EClassJob _classJob;

    public ITask With(EClassJob classJob)
    {
        _classJob = classJob;
        return this;
    }

    protected override unsafe bool StartInternal()
    {
        if (clientState.LocalPlayer!.ClassJob.Id == (uint)_classJob)
            return false;

        var gearsetModule = RaptureGearsetModule.Instance();
        if (gearsetModule != null)
        {
            for (int i = 0; i < 100; ++i)
            {
                var gearset = gearsetModule->GetGearset(i);
                if (gearset->ClassJob == (byte)_classJob)
                {
                    gearsetModule->EquipGearset(gearset->Id, gearset->BannerIndex);
                    return true;
                }
            }
        }

        throw new TaskException($"No gearset found for {_classJob}");
    }

    protected override ETaskResult UpdateInternal() => ETaskResult.TaskComplete;

    public override string ToString() => $"SwitchJob({_classJob})";
}
