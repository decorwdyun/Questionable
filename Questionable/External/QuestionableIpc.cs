using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Questionable.Controller;
using Questionable.Windows.QuestComponents;

namespace Questionable.External;

internal sealed class QuestionableIpc : IDisposable
{
    private const string IpcIsRunning = "Questionable.IsRunning";
    private const string IpcGetCurrentQuestId = "Questionable.GetCurrentQuestId";
    private const string IpcGetCurrentlyActiveEventQuests = "Questionable.GetCurrentlyActiveEventQuests";

    private readonly ICallGateProvider<bool> _isRunning;
    private readonly ICallGateProvider<string?> _getCurrentQuestId;
    private readonly ICallGateProvider<List<string>> _getCurrentlyActiveEventQuests;

    public QuestionableIpc(
        QuestController questController,
        EventInfoComponent eventInfoComponent,
        IDalamudPluginInterface pluginInterface)
    {
        _isRunning = pluginInterface.GetIpcProvider<bool>(IpcIsRunning);
        _isRunning.RegisterFunc(() =>
            questController.AutomationType != QuestController.EAutomationType.Manual || questController.IsRunning);

        _getCurrentQuestId = pluginInterface.GetIpcProvider<string?>(IpcGetCurrentQuestId);
        _getCurrentQuestId.RegisterFunc(() => questController.CurrentQuest?.Quest.Id.ToString());

        _getCurrentlyActiveEventQuests =
            pluginInterface.GetIpcProvider<List<string>>(IpcGetCurrentlyActiveEventQuests);
        _getCurrentlyActiveEventQuests.RegisterFunc(() =>
            eventInfoComponent.GetCurrentlyActiveEventQuests().Select(q => q.ToString()).ToList());
    }


    public void Dispose()
    {
        _getCurrentlyActiveEventQuests.UnregisterFunc();
        _getCurrentQuestId.UnregisterFunc();
        _isRunning.UnregisterFunc();
    }
}
