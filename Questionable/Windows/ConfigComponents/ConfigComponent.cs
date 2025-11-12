using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;

namespace Questionable.Windows.ConfigComponents;

internal abstract class ConfigComponent(IDalamudPluginInterface pluginInterface, Configuration configuration)
{
    protected const string DutyClipboardSeparator = ";";
    protected const string DutyWhitelistPrefix = "+";
    protected const string DutyBlacklistPrefix = "-";

    protected readonly string[] SupportedCfcOptions =
    [
        $"{SeIconChar.Circle.ToIconChar()} 启用 (默认)",
        $"{SeIconChar.Circle.ToIconChar()} 启用",
        $"{SeIconChar.Cross.ToIconChar()} 禁用"
    ];

    protected readonly string[] UnsupportedCfcOptions =
    [
        $"{SeIconChar.Cross.ToIconChar()} 禁用 (默认)",
        $"{SeIconChar.Circle.ToIconChar()} 启用",
        $"{SeIconChar.Cross.ToIconChar()} 禁用"
    ];

    private readonly IDalamudPluginInterface _pluginInterface = pluginInterface;

    protected Configuration Configuration { get; } = configuration;

    public abstract void DrawTab();

    protected void Save() => _pluginInterface.SavePluginConfig(Configuration);

    protected static string FormatLevel(int level, bool includePrefix = true)
    {
        if (level == 0)
            return string.Empty;

        return $"{(includePrefix ? SeIconChar.LevelEn.ToIconString() : string.Empty)}{FormatLevel(level / 10, false)}{(SeIconChar.Number0 + level % 10).ToIconChar()}";
    }

    protected static void DrawNotes(bool enabledByDefault, IEnumerable<string> notes)
    {
        using var color = new ImRaii.Color();
        color.Push(ImGuiCol.TextDisabled, !enabledByDefault ? ImGuiColors.DalamudYellow : ImGuiColors.ParsedBlue);

        ImGui.SameLine();
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            if (!enabledByDefault)
                ImGui.TextDisabled(FontAwesomeIcon.ExclamationTriangle.ToIconString());
            else
                ImGui.TextDisabled(FontAwesomeIcon.InfoCircle.ToIconString());
        }

        if (!ImGui.IsItemHovered())
            return;

        using var _ = ImRaii.Tooltip();

        ImGui.TextColored(ImGuiColors.DalamudYellow,
            "在我们测试时发现了以下问题:");
        foreach (string note in notes)
            ImGui.BulletText(note);
    }
}
