using System;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImGuiNET;
using LLib.ImGui;
using Questionable.Controller;
using Questionable.Data;
using Questionable.Windows.QuestComponents;

namespace Questionable.Windows;

internal sealed class QuestWindow : LWindow, IPersistableWindowConfig
{
    private static readonly Version PluginVersion = typeof(QuestionablePlugin).Assembly.GetName().Version!;

    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly QuestController _questController;
    private readonly IClientState _clientState;
    private readonly Configuration _configuration;
    private readonly TerritoryData _territoryData;
    private readonly ActiveQuestComponent _activeQuestComponent;
    private readonly ARealmRebornComponent _aRealmRebornComponent;
    private readonly CreationUtilsComponent _creationUtilsComponent;
    private readonly QuickAccessButtonsComponent _quickAccessButtonsComponent;
    private readonly RemainingTasksComponent _remainingTasksComponent;
    private readonly IFramework _framework;
    private readonly GameUiController _gameUiController;
    private readonly TitleBarButton _minimizeButton;

    public QuestWindow(IDalamudPluginInterface pluginInterface,
        QuestController questController,
        IClientState clientState,
        Configuration configuration,
        TerritoryData territoryData,
        ActiveQuestComponent activeQuestComponent,
        ARealmRebornComponent aRealmRebornComponent,
        CreationUtilsComponent creationUtilsComponent,
        QuickAccessButtonsComponent quickAccessButtonsComponent,
        RemainingTasksComponent remainingTasksComponent,
        IFramework framework,
        GameUiController gameUiController)
        : base($"Questionable v{PluginVersion.ToString(2)}###Questionable",
            ImGuiWindowFlags.AlwaysAutoResize)
    {
        _pluginInterface = pluginInterface;
        _questController = questController;
        _clientState = clientState;
        _configuration = configuration;
        _territoryData = territoryData;
        _activeQuestComponent = activeQuestComponent;
        _aRealmRebornComponent = aRealmRebornComponent;
        _creationUtilsComponent = creationUtilsComponent;
        _quickAccessButtonsComponent = quickAccessButtonsComponent;
        _remainingTasksComponent = remainingTasksComponent;
        _framework = framework;
        _gameUiController = gameUiController;

#if DEBUG
        IsOpen = true;
#endif
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(200, 30),
            MaximumSize = default
        };
        RespectCloseHotkey = false;
        AllowClickthrough = false;

        _minimizeButton = new TitleBarButton
        {
            Icon = FontAwesomeIcon.Minus,
            Priority = int.MinValue,
            IconOffset = new Vector2(1.5f, 1),
            Click = _ =>
            {
                IsMinimized = !IsMinimized;
                _minimizeButton!.Icon = IsMinimized ? FontAwesomeIcon.WindowMaximize : FontAwesomeIcon.Minus;
            },
            AvailableClickthrough = true,
        };
        TitleBarButtons.Insert(0, _minimizeButton);

        _activeQuestComponent.Reload += OnReload;
        _quickAccessButtonsComponent.Reload += OnReload;
    }

    public WindowConfig WindowConfig => _configuration.DebugWindowConfig;
    public bool IsMinimized { get; set; }

    public void SaveWindowConfig() => _pluginInterface.SavePluginConfig(_configuration);

    public override void PreOpenCheck()
    {
        if (_questController.IsRunning)
        {
            IsOpen = true;
            Flags |= ImGuiWindowFlags.NoCollapse;
            ShowCloseButton = false;
        }
        else
        {
            Flags &= ~ImGuiWindowFlags.NoCollapse;
            ShowCloseButton = true;
        }
    }

    public override bool DrawConditions()
    {
        if (!_clientState.IsLoggedIn || _clientState.LocalPlayer == null || _clientState.IsPvPExcludingDen)
            return false;

        if (_configuration.General.HideInAllInstances && _territoryData.IsDutyInstance(_clientState.TerritoryType))
            return false;

        var currentQuest = _questController.CurrentQuest;
        return currentQuest == null || !currentQuest.Quest.Root.TerritoryBlacklist.Contains(_clientState.TerritoryType);
    }

    public override void Draw()
    {
        _activeQuestComponent.Draw(IsMinimized);
        if (!IsMinimized)
        {
            ImGui.Separator();

            if (_aRealmRebornComponent.ShouldDraw)
            {
                _aRealmRebornComponent.Draw();
                ImGui.Separator();
            }

            _creationUtilsComponent.Draw();
            ImGui.Separator();

            _quickAccessButtonsComponent.Draw();
            _remainingTasksComponent.Draw();
        }
    }

    private void OnReload(object? sender, EventArgs e) => Reload();

    internal void Reload()
    {
        _questController.Reload();
        _framework.RunOnTick(() => _gameUiController.HandleCurrentDialogueChoices(),
            TimeSpan.FromMilliseconds(200));
    }
}
