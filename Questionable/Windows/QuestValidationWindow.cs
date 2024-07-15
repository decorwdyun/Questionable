using System.Globalization;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Common.Math;
using ImGuiNET;
using LLib.ImGui;
using Questionable.Data;
using Questionable.Model;
using Questionable.Validation;

namespace Questionable.Windows;

internal sealed class QuestValidationWindow : LWindow
{
    private readonly QuestValidator _questValidator;
    private readonly QuestData _questData;

    public QuestValidationWindow(QuestValidator questValidator, QuestData questData) : base("Quest Validation###QuestionableValidator")
    {
        _questValidator = questValidator;
        _questData = questData;

        Size = new Vector2(600, 200);
        SizeCondition = ImGuiCond.Once;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(600, 200),
        };
    }

    public override void Draw()
    {
        using var table = ImRaii.Table("QuestSelection", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY);
        if (!table)
        {
            ImGui.Text("Not table");
            return;
        }

        ImGui.TableSetupColumn("Quest", ImGuiTableColumnFlags.WidthFixed, 50);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 200);
        ImGui.TableSetupColumn("Sq", ImGuiTableColumnFlags.WidthFixed, 30);
        ImGui.TableSetupColumn("Sp", ImGuiTableColumnFlags.WidthFixed, 30);
        ImGui.TableSetupColumn("Issue", ImGuiTableColumnFlags.None, 200);
        ImGui.TableHeadersRow();

        foreach (ValidationIssue validationIssue in _questValidator.Issues)
        {
            ImGui.TableNextRow();

            if (ImGui.TableNextColumn())
                ImGui.TextUnformatted(validationIssue.QuestId.ToString(CultureInfo.InvariantCulture));

            if (ImGui.TableNextColumn())
                ImGui.TextUnformatted(_questData.GetQuestInfo(validationIssue.QuestId).Name);

            if (ImGui.TableNextColumn())
                ImGui.TextUnformatted(validationIssue.Sequence?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);

            if (ImGui.TableNextColumn())
                ImGui.TextUnformatted(validationIssue.Step?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);

            if (ImGui.TableNextColumn())
                ImGui.TextUnformatted(validationIssue.Description);
        }
    }
}
