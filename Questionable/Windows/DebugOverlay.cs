using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ImGuiNET;
using Questionable.Controller;
using Questionable.Model.V1;

namespace Questionable.Windows;

internal sealed class DebugOverlay : Window
{
    private readonly QuestController _questController;
    private readonly IGameGui _gameGui;
    private readonly Configuration _configuration;

    public DebugOverlay(QuestController questController, IGameGui gameGui, Configuration configuration)
        : base("Questionable Debug Overlay###QuestionableDebugOverlay",
            ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground |
            ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoSavedSettings, true)
    {
        _questController = questController;
        _gameGui = gameGui;
        _configuration = configuration;

        Position = Vector2.Zero;
        PositionCondition = ImGuiCond.Always;
        Size = ImGui.GetIO().DisplaySize;
        SizeCondition = ImGuiCond.Always;
        IsOpen = true;
    }

    public override bool DrawConditions() => _configuration.Advanced.DebugOverlay;

    public override void PreDraw()
    {
        Size = ImGui.GetIO().DisplaySize;
    }

    public override void Draw()
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
            if (step == null || step.Position == null)
                continue;

            bool visible = _gameGui.WorldToScreen(step.Position.Value, out Vector2 screenPos);
            if (!visible)
                continue;

            ImGui.GetWindowDrawList().AddCircleFilled(screenPos, 3f, 0xFF0000FF);
            ImGui.GetWindowDrawList().AddText(screenPos + new Vector2(10, -8), 0xFF0000FF,
                $"{i}: {step.InteractionType}\n{step.Position.Value.ToString("G", CultureInfo.InvariantCulture)}\n{step.Comment}");
        }
    }
}
