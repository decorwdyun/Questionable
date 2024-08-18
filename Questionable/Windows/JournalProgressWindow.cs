﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImGuiNET;
using LLib.ImGui;
using Questionable.Controller;
using Questionable.Data;
using Questionable.Functions;
using Questionable.Model;
using Questionable.Windows.JournalComponents;
using Questionable.Windows.QuestComponents;

namespace Questionable.Windows;

internal sealed class JournalProgressWindow : LWindow, IDisposable
{
    private readonly QuestJournalComponent _questJournalComponent;
    private readonly GatheringJournalComponent _gatheringJournalComponent;
    private readonly QuestRegistry _questRegistry;
    private readonly IClientState _clientState;

    public JournalProgressWindow(
        QuestJournalComponent questJournalComponent,
        GatheringJournalComponent gatheringJournalComponent,
        QuestRegistry questRegistry,
        IClientState clientState)
        : base("Journal Progress###QuestionableJournalProgress")
    {
        _questJournalComponent = questJournalComponent;
        _gatheringJournalComponent = gatheringJournalComponent;
        _questRegistry = questRegistry;
        _clientState = clientState;

        _clientState.Login += _questJournalComponent.RefreshCounts;
        _clientState.Login += _gatheringJournalComponent.RefreshCounts;
        _clientState.Logout -= _questJournalComponent.ClearCounts;
        _questRegistry.Reloaded += OnQuestsReloaded;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(700, 500)
        };
    }

    private void OnQuestsReloaded(object? sender, EventArgs e)
    {
        _questJournalComponent.RefreshCounts();
        _gatheringJournalComponent.RefreshCounts();
    }

    public override void OnOpen()
    {
        _questJournalComponent.UpdateFilter();
        _questJournalComponent.RefreshCounts();
        _gatheringJournalComponent.RefreshCounts();
    }

    public override void Draw()
    {
        using var tabBar = ImRaii.TabBar("Journal");
        if (!tabBar)
            return;

        _questJournalComponent.DrawQuests();
        _gatheringJournalComponent.DrawGatheringItems();
    }

    public void Dispose()
    {
        _questRegistry.Reloaded -= OnQuestsReloaded;
        _clientState.Logout -= _questJournalComponent.ClearCounts;
        _clientState.Login -= _gatheringJournalComponent.RefreshCounts;
        _clientState.Login -= _questJournalComponent.RefreshCounts;
    }
}
