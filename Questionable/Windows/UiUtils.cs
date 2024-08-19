using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using ImGuiNET;
using Questionable.Functions;
using Questionable.Model.Questing;

namespace Questionable.Windows;

internal sealed class UiUtils
{
    private readonly QuestFunctions _questFunctions;
    private readonly IDalamudPluginInterface _pluginInterface;

    public UiUtils(QuestFunctions questFunctions, IDalamudPluginInterface pluginInterface)
    {
        _questFunctions = questFunctions;
        _pluginInterface = pluginInterface;
    }

    public (Vector4 Color, FontAwesomeIcon Icon, string Status) GetQuestStyle(ElementId elementId)
    {
        if (_questFunctions.IsQuestAccepted(elementId))
            return (ImGuiColors.DalamudYellow, FontAwesomeIcon.PersonWalkingArrowRight, "Active");
        else if (_questFunctions.IsQuestAcceptedOrComplete(elementId))
            return (ImGuiColors.ParsedGreen, FontAwesomeIcon.Check, "Complete");
        else if (_questFunctions.IsQuestLocked(elementId))
            return (ImGuiColors.DalamudRed, FontAwesomeIcon.Times, "Locked");
        else
            return (ImGuiColors.DalamudYellow, FontAwesomeIcon.Running, "Available");
    }

    public static (Vector4 color, FontAwesomeIcon icon) GetInstanceStyle(ushort instanceId)
    {
        if (UIState.IsInstanceContentCompleted(instanceId))
            return (ImGuiColors.ParsedGreen, FontAwesomeIcon.Check);
        else if (UIState.IsInstanceContentUnlocked(instanceId))
            return (ImGuiColors.DalamudYellow, FontAwesomeIcon.Running);
        else
            return (ImGuiColors.DalamudRed, FontAwesomeIcon.Times);
    }

    public bool ChecklistItem(string text, Vector4 color, FontAwesomeIcon icon, float extraPadding = 0)
    {
        // ReSharper disable once UnusedVariable
        using (var font = _pluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
        {
            ImGui.TextColored(color, icon.ToIconString());
        }

        bool hover = ImGui.IsItemHovered();

        ImGui.SameLine();
        if (extraPadding > 0)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + extraPadding);
        ImGui.TextUnformatted(text);
        return hover;
    }

    public bool ChecklistItem(string text, bool complete)
    {
        return ChecklistItem(text,
            complete ? ImGuiColors.ParsedGreen : ImGuiColors.DalamudRed,
            complete ? FontAwesomeIcon.Check : FontAwesomeIcon.Times);
    }
}
