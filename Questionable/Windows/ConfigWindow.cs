using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using LLib.ImGui;
using Questionable.Windows.ConfigComponents;

namespace Questionable.Windows;

internal sealed class ConfigWindow(
    IDalamudPluginInterface pluginInterface,
    GeneralConfigComponent generalConfigComponent,
    PluginConfigComponent pluginConfigComponent,
    DutyConfigComponent dutyConfigComponent,
    SinglePlayerDutyConfigComponent singlePlayerDutyConfigComponent,
    StopConditionComponent stopConditionComponent,
    NotificationConfigComponent notificationConfigComponent,
    DebugConfigComponent debugConfigComponent,
    AboutConfigComponent aboutConfigComponent,
    Configuration configuration) : LWindow("Config - Questionable###QuestionableConfig", ImGuiWindowFlags.AlwaysAutoResize), IPersistableWindowConfig
{
    private readonly IDalamudPluginInterface _pluginInterface = pluginInterface;
    private readonly GeneralConfigComponent _generalConfigComponent = generalConfigComponent;
    private readonly PluginConfigComponent _pluginConfigComponent = pluginConfigComponent;
    private readonly DutyConfigComponent _dutyConfigComponent = dutyConfigComponent;
    private readonly SinglePlayerDutyConfigComponent _singlePlayerDutyConfigComponent = singlePlayerDutyConfigComponent;
    private readonly StopConditionComponent _stopConditionComponent = stopConditionComponent;
    private readonly NotificationConfigComponent _notificationConfigComponent = notificationConfigComponent;
    private readonly DebugConfigComponent _debugConfigComponent = debugConfigComponent;
    private readonly Configuration _configuration = configuration;
    private readonly AboutConfigComponent _aboutConfigComponent= aboutConfigComponent;
    public WindowConfig WindowConfig => _configuration.ConfigWindowConfig;

    public override void DrawContent()
    {
        using var tabBar = ImRaii.TabBar("QuestionableConfigTabs");
        if (!tabBar)
            return;

        _generalConfigComponent.DrawTab();
        _pluginConfigComponent.DrawTab();
        _dutyConfigComponent.DrawTab();
        _singlePlayerDutyConfigComponent.DrawTab();
        _stopConditionComponent.DrawTab();
        _notificationConfigComponent.DrawTab();
        _debugConfigComponent.DrawTab();
        _aboutConfigComponent.DrawTab();
    }

    public void SaveWindowConfig() => _pluginInterface.SavePluginConfig(_configuration);
}
