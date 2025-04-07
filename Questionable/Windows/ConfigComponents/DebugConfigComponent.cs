using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using ImGuiNET;

namespace Questionable.Windows.ConfigComponents;

internal sealed class DebugConfigComponent : ConfigComponent
{
    public DebugConfigComponent(IDalamudPluginInterface pluginInterface, Configuration configuration)
        : base(pluginInterface, configuration)
    {
    }

    public override void DrawTab()
    {
        using var tab = ImRaii.TabItem("Advanced###Debug");
        if (!tab)
            return;

        ImGui.TextColored(ImGuiColors.DalamudRed,
            "Enabling any option here may cause unexpected behavior. Use at your own risk.");

        ImGui.Separator();

        bool debugOverlay = Configuration.Advanced.DebugOverlay;
        if (ImGui.Checkbox("Enable debug overlay", ref debugOverlay))
        {
            Configuration.Advanced.DebugOverlay = debugOverlay;
            Save();
        }

        using (ImRaii.Disabled(!debugOverlay))
        {
            using (ImRaii.PushIndent())
            {
                bool combatDataOverlay = Configuration.Advanced.CombatDataOverlay;
                if (ImGui.Checkbox("Enable combat data overlay", ref combatDataOverlay))
                {
                    Configuration.Advanced.CombatDataOverlay = combatDataOverlay;
                    Save();
                }
            }
        }

        bool neverFly = Configuration.Advanced.NeverFly;
        if (ImGui.Checkbox("Disable flying (even if unlocked for the zone)", ref neverFly))
        {
            Configuration.Advanced.NeverFly = neverFly;
            Save();
        }

        bool additionalStatusInformation = Configuration.Advanced.AdditionalStatusInformation;
        if (ImGui.Checkbox("Draw additional status information", ref additionalStatusInformation))
        {
            Configuration.Advanced.AdditionalStatusInformation = additionalStatusInformation;
            Save();
        }

        ImGui.Separator();

        ImGui.Text("AutoDuty options");
        using (var _ = ImRaii.PushIndent())
        {
            ImGui.AlignTextToFramePadding();
            bool disableAutoDutyBareMode = Configuration.Advanced.DisableAutoDutyBareMode;
            if (ImGui.Checkbox("Use Pre-Loop/Loop/Post-Loop settings", ref disableAutoDutyBareMode))
            {
                Configuration.Advanced.DisableAutoDutyBareMode = disableAutoDutyBareMode;
                Save();
            }

            ImGui.SameLine();
            ImGuiComponents.HelpMarker(
                "Typically, the loop settings for AutoDuty are disabled when running dungeons with Questionable, since they can cause issues (or even shut down your PC).");
        }

        ImGui.EndTabItem();
    }
}
