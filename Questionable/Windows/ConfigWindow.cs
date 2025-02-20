using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using ImGuiNET;
using LLib.ImGui;
using Questionable.Windows.ConfigComponents;

namespace Questionable.Windows;

internal sealed class ConfigWindow : LWindow, IPersistableWindowConfig
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly GeneralConfigComponent _generalConfigComponent;
    private readonly DutyConfigComponent _dutyConfigComponent;
    private readonly NotificationConfigComponent _notificationConfigComponent;
    private readonly DebugConfigComponent _debugConfigComponent;
    private readonly Configuration _configuration;

    public ConfigWindow(
        IDalamudPluginInterface pluginInterface,
        GeneralConfigComponent generalConfigComponent,
        DutyConfigComponent dutyConfigComponent,
        NotificationConfigComponent notificationConfigComponent,
        DebugConfigComponent debugConfigComponent,
        Configuration configuration)
        : base("Config - Questionable###QuestionableConfig", ImGuiWindowFlags.AlwaysAutoResize)
    {
        _pluginInterface = pluginInterface;
        _generalConfigComponent = generalConfigComponent;
        _dutyConfigComponent = dutyConfigComponent;
        _notificationConfigComponent = notificationConfigComponent;
        _debugConfigComponent = debugConfigComponent;
        _configuration = configuration;
    }

    public WindowConfig WindowConfig => _configuration.ConfigWindowConfig;

    public override void Draw()
    {
        using var tabBar = ImRaii.TabBar("QuestionableConfigTabs");
        if (!tabBar)
            return;

        _generalConfigComponent.DrawTab();
        _dutyConfigComponent.DrawTab();
        _notificationConfigComponent.DrawTab();
        _debugConfigComponent.DrawTab();
    }

    public void SaveWindowConfig() => _pluginInterface.SavePluginConfig(_configuration);
}
