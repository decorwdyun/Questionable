using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ImGuiNET;
using Questionable.Controller;
using Questionable.Model;
using Questionable.Model.V1;

namespace Questionable.Windows;

internal sealed class DebugOverlay : Window
{
    private readonly QuestController _questController;
    private readonly QuestRegistry _questRegistry;
    private readonly IGameGui _gameGui;
    private readonly IClientState _clientState;
    private readonly Configuration _configuration;

    public DebugOverlay(QuestController questController, QuestRegistry questRegistry, IGameGui gameGui,
        IClientState clientState, Configuration configuration)
        : base("Questionable Debug Overlay###QuestionableDebugOverlay",
            ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground |
            ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoSavedSettings, true)
    {
        _questController = questController;
        _questRegistry = questRegistry;
        _gameGui = gameGui;
        _clientState = clientState;
        _configuration = configuration;

        Position = Vector2.Zero;
        PositionCondition = ImGuiCond.Always;
        Size = ImGui.GetIO().DisplaySize;
        SizeCondition = ImGuiCond.Always;
        IsOpen = true;
    }

    public ushort? HighlightedQuest { get; set; }

    public override bool DrawConditions() => _configuration.Advanced.DebugOverlay;

    public override void PreDraw()
    {
        Size = ImGui.GetIO().DisplaySize;
    }

    public override void Draw()
    {
        DrawCurrentQuest();
        DrawHighlightedQuest();
    }

    private void DrawCurrentQuest()
    {
        var currentQuest = _questController.CurrentQuest;
        if (currentQuest == null)
            return;

        var sequence = currentQuest.Quest.FindSequence(currentQuest.Sequence);
        if (sequence == null)
            return;

        for (int i = currentQuest.Step; i <= sequence.Steps.Count; ++i)
        {
            QuestStep? step = sequence.FindStep(i);
            DrawStep(i.ToString(CultureInfo.InvariantCulture), step);
        }
    }

    private void DrawHighlightedQuest()
    {
        if (HighlightedQuest == null || !_questRegistry.TryGetQuest(HighlightedQuest.Value, out var quest))
            return;

        foreach (var sequence in quest.Root.QuestSequence)
        {
            for (int i = 0; i < sequence.Steps.Count; ++i)
            {
                QuestStep? step = sequence.FindStep(i);
                DrawStep($"{quest.QuestId} / {sequence.Sequence} / {i}", step, 0xFFFFFFFF);
            }
        }
    }

    private void DrawStep(string counter, QuestStep? step, uint color = 0xFF0000FF)
    {
        if (step == null ||
            step.Position == null ||
            step.Disabled ||
            step.TerritoryId != _clientState.TerritoryType)
            return;

        bool visible = _gameGui.WorldToScreen(step.Position.Value, out Vector2 screenPos);
        if (!visible)
            return;

        ImGui.GetWindowDrawList().AddCircleFilled(screenPos, 3f, color);
        ImGui.GetWindowDrawList().AddText(screenPos + new Vector2(10, -8), color,
            $"{counter}: {step.InteractionType}\n{step.Position.Value.ToString("G", CultureInfo.InvariantCulture)}\n{step.Comment}");
    }
}
