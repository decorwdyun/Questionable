﻿using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using ImGuiNET;
using Questionable.Model.Questing;

namespace Questionable.Windows;

internal sealed class UiUtils
{
    private readonly GameFunctions _gameFunctions;
    private readonly IDalamudPluginInterface _pluginInterface;

    public UiUtils(GameFunctions gameFunctions, IDalamudPluginInterface pluginInterface)
    {
        _gameFunctions = gameFunctions;
        _pluginInterface = pluginInterface;
    }

    public (Vector4 color, FontAwesomeIcon icon, string status) GetQuestStyle(ElementId questElementId)
    {
        if (_gameFunctions.IsQuestAccepted(questElementId))
            return (ImGuiColors.DalamudYellow, FontAwesomeIcon.PersonWalkingArrowRight, "Active");
        else if (_gameFunctions.IsQuestAcceptedOrComplete(questElementId))
            return (ImGuiColors.ParsedGreen, FontAwesomeIcon.Check, "Complete");
        else if (_gameFunctions.IsQuestLocked(questElementId))
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

    public bool ChecklistItem(string text, Vector4 color, FontAwesomeIcon icon)
    {
        // ReSharper disable once UnusedVariable
        using (var font = _pluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
        {
            ImGui.TextColored(color, icon.ToIconString());
        }

        bool hover = ImGui.IsItemHovered();

        ImGui.SameLine();
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
