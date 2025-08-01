using System;
using System.Linq;
using Dalamud.Game.Text;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Utility;
using ImGuiNET;
using Questionable.External;

namespace Questionable.Windows.ConfigComponents;

internal sealed class NotificationConfigComponent : ConfigComponent
{
    private readonly NotificationMasterIpc _notificationMasterIpc;

    public NotificationConfigComponent(
        IDalamudPluginInterface pluginInterface,
        Configuration configuration,
        NotificationMasterIpc notificationMasterIpc)
        : base(pluginInterface, configuration)
    {
        _notificationMasterIpc = notificationMasterIpc;
    }

    public override void DrawTab()
    {
        using var tab = ImRaii.TabItem("通知###Notifications");
        if (!tab)
            return;

        bool enabled = Configuration.Notifications.Enabled;
        if (ImGui.Checkbox("需要【手动交互】时发送通知", ref enabled))
        {
            Configuration.Notifications.Enabled = enabled;
            Save();
        }

        using (ImRaii.Disabled(!Configuration.Notifications.Enabled))
        {
            using (ImRaii.PushIndent())
            {
                var xivChatTypes = Enum.GetValues<XivChatType>()
                    .Where(x => x != XivChatType.StandardEmote)
                    .ToArray();
                var selectedChatType = Array.IndexOf(xivChatTypes, Configuration.Notifications.ChatType);
                string[] chatTypeNames = xivChatTypes
                    .Select(t => t.GetAttribute<XivChatTypeInfoAttribute>()?.FancyName ?? t.ToString())
                    .ToArray();
                if (ImGui.Combo("聊天频道", ref selectedChatType, chatTypeNames,
                        chatTypeNames.Length))
                {
                    Configuration.Notifications.ChatType = xivChatTypes[selectedChatType];
                    Save();
                }

                ImGui.Separator();
                ImGui.Text("NotificationMaster 设置");
                ImGui.SameLine();
                ImGuiComponents.HelpMarker("需要安装 NotificationMaster 插件。");
                using (ImRaii.Disabled(!_notificationMasterIpc.Enabled))
                {
                    bool showTrayMessage = Configuration.Notifications.ShowTrayMessage;
                    if (ImGui.Checkbox("显示托盘通知", ref showTrayMessage))
                    {
                        Configuration.Notifications.ShowTrayMessage = showTrayMessage;
                        Save();
                    }

                    bool flashTaskbar = Configuration.Notifications.FlashTaskbar;
                    if (ImGui.Checkbox("闪烁任务栏图标", ref flashTaskbar))
                    {
                        Configuration.Notifications.FlashTaskbar = flashTaskbar;
                        Save();
                    }
                }
            }
        }
    }
}
