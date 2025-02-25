using System.Text;
using Dalamud.Game.Text;
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
}
