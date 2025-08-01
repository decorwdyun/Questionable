using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using ImGuiNET;
using LLib.ImGui;
using Microsoft.Extensions.Logging;
using Questionable.Windows.ConfigComponents;

namespace Questionable.Windows;

internal sealed class OneTimeSetupWindow : LWindow
{
    private readonly PluginConfigComponent _pluginConfigComponent;
    private readonly Configuration _configuration;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly ILogger<OneTimeSetupWindow> _logger;
    private readonly string _authToken = "倒卖免费插件的小店死个妈 买的也是傻逼";
    private string _authTokenInput = "";
    public OneTimeSetupWindow(
        PluginConfigComponent pluginConfigComponent,
        Configuration configuration,
        IDalamudPluginInterface pluginInterface,
        ILogger<OneTimeSetupWindow> logger)
        : base("Questionable Setup###QuestionableOneTimeSetup",
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings, true)
    {
        _pluginConfigComponent = pluginConfigComponent;
        _configuration = configuration;
        _pluginInterface = pluginInterface;
        _logger = logger;

        RespectCloseHotkey = false;
        ShowCloseButton = false;
        AllowPinning = false;
        AllowClickthrough = false;
        IsOpen = !_configuration.IsPluginSetupComplete();
        _logger.LogInformation("One-time setup needed: {IsOpen}", IsOpen);
    }

    public override void DrawContent()
    {
        _pluginConfigComponent.Draw(out bool allRequiredInstalled);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (allRequiredInstalled)
        {
            ImGui.Text("请在下方输入口令：");
            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.DalamudRed, _authToken);
            ImGui.SameLine();
            ImGui.Text("来完成设置（中间的空格也要输入）：");
            ImGui.InputText("", ref _authTokenInput, 200);
        }

        if (allRequiredInstalled && _authToken == _authTokenInput)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.ParsedGreen))
            {
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Check, "完成配置"))
                {
                    _logger.LogInformation("Marking setup as complete");
                    _configuration.MarkPluginSetupComplete();
                    _pluginInterface.SavePluginConfig(_configuration);
                    IsOpen = false;
                }
            }
        }
        else
        {
            using (ImRaii.Disabled())
            {
                using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudRed))
                {
                    var warningText = "缺少必需的插件";
                    if (allRequiredInstalled)
                        warningText = "请输入正确的验证码";
                    ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Check, warningText);
                }
            }
        }

        ImGui.SameLine();

        if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Times, "关闭此窗口并且不启用 Questionable"))
        {
            _logger.LogWarning("Closing window without all required plugins installed");
            IsOpen = false;
        }
    }
}
