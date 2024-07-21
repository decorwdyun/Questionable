using ImGuiNET;
using Questionable.Controller;

namespace Questionable.Windows.QuestComponents;

internal sealed class RemainingTasksComponent
{
    private readonly QuestController _questController;

    public RemainingTasksComponent(QuestController questController)
    {
        _questController = questController;
    }

    public void Draw()
    {
        var remainingTasks = _questController.GetRemainingTaskNames();
        if (remainingTasks.Count > 0)
        {
            ImGui.Separator();
            ImGui.BeginDisabled();
            foreach (var task in remainingTasks)
                ImGui.TextUnformatted(task);
            ImGui.EndDisabled();
        }
    }
}
