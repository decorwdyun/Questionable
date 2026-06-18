using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Utility;
using Questionable.External;
using static Questionable.Utils.LocalizeShortcut;
namespace Questionable.Windows.ConfigComponents;

internal sealed class NotificationConfigComponent
(
    IDalamudPluginInterface pluginInterface,
    Configuration configuration,
    NotificationMasterIpc notificationMasterIpc) : ConfigComponent(pluginInterface, configuration)
{

    public override void DrawTab()
    {
        using ImRaii.TabItemDisposable tab = ImRaii.TabItem(_L("通知") + "###Notifications");
        if (!tab)
            return;

        bool enabled = Configuration.Notifications.Enabled;
        if (ImGui.Checkbox(_L("需要手动操作时启用通知"), ref enabled))
        {
            Configuration.Notifications.Enabled = enabled;
            Save();
        }

        using (ImRaii.Disabled(!Configuration.Notifications.Enabled))
        {
            using (ImRaii.PushIndent())
            {
                XivChatType[] xivChatTypes = Enum.GetValues<XivChatType>()
                    .Where(x => x != XivChatType.StandardEmote)
                    .ToArray();
                string[] chatTypeNames = xivChatTypes
                    .Select(t => t.GetAttribute<XivChatTypeInfoAttribute>()?.FancyName ?? t.ToString())
                    .ToArray();
                DrawComboOption(_L("聊天频道"), xivChatTypes, chatTypeNames,
                    () => Configuration.Notifications.ChatType,
                    v => Configuration.Notifications.ChatType = v);


                ImGui.Separator();
                ImGui.Text(_L("桌面通知"));
                ImGui.SameLine();
                ImGuiComponents.HelpMarker(_L("需要安装 NotificationMaster 插件。"));
                using (ImRaii.Disabled(!notificationMasterIpc.Enabled))
                {
                    bool showTrayMessage = Configuration.Notifications.ShowTrayMessage;
                    if (ImGui.Checkbox(_L("显示托盘通知"), ref showTrayMessage))
                    {
                        Configuration.Notifications.ShowTrayMessage = showTrayMessage;
                        Save();
                    }

                    bool flashTaskbar = Configuration.Notifications.FlashTaskbar;
                    if (ImGui.Checkbox(_L("闪烁任务栏图标"), ref flashTaskbar))
                    {
                        Configuration.Notifications.FlashTaskbar = flashTaskbar;
                        Save();
                    }
                }
            }
        }
    }
}
