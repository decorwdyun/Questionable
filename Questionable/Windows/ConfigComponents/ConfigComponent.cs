using System.Collections.Generic;
using System.Text;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using ImGuiNET;

namespace Questionable.Windows.ConfigComponents;

internal abstract class ConfigComponent
{
    protected const string DutyClipboardSeparator = ";";
    protected const string DutyWhitelistPrefix = "+";
    protected const string DutyBlacklistPrefix = "-";

    protected readonly string[] SupportedCfcOptions =
    [
        $"{SeIconChar.Circle.ToIconChar()} Enabled (Default)",
        $"{SeIconChar.Circle.ToIconChar()} Enabled",
        $"{SeIconChar.Cross.ToIconChar()} Disabled"
    ];

    protected readonly string[] UnsupportedCfcOptions =
    [
        $"{SeIconChar.Cross.ToIconChar()} Disabled (Default)",
        $"{SeIconChar.Circle.ToIconChar()} Enabled",
        $"{SeIconChar.Cross.ToIconChar()} Disabled"
    ];

    private readonly IDalamudPluginInterface _pluginInterface;

    protected ConfigComponent(IDalamudPluginInterface pluginInterface, Configuration configuration)
    {
        _pluginInterface = pluginInterface;
        Configuration = configuration;
    }

    protected Configuration Configuration { get; }

    public abstract void DrawTab();

    protected void Save() => _pluginInterface.SavePluginConfig(Configuration);

    protected static string FormatLevel(int level, bool includePrefix = true)
    {
        if (level == 0)
            return string.Empty;

        return $"{(includePrefix ? SeIconChar.LevelEn.ToIconString() : string.Empty)}{FormatLevel(level / 10, false)}{(SeIconChar.Number0 + level % 10).ToIconChar()}";
    }

    /// <summary>
    /// The default implementation for <see cref="ImGui.GetClipboardText"/> throws an NullReferenceException if the clipboard is empty, maybe also if it doesn't contain text.
    /// </summary>
    protected unsafe string? GetClipboardText()
    {
        byte* ptr = ImGuiNative.igGetClipboardText();
        if (ptr == null)
            return null;

        int byteCount = 0;
        while (ptr[byteCount] != 0)
            ++byteCount;
        return Encoding.UTF8.GetString(ptr, byteCount);
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
            "While testing, the following issues have been found:");
        foreach (string note in notes)
            ImGui.BulletText(note);
    }
}
