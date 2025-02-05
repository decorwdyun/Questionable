using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Questionable.Controller;
using Questionable.Model.Questing;
using Questionable.Windows.QuestComponents;

namespace Questionable.External;

internal sealed class QuestionableIpc : IDisposable
{
    private const string IpcIsRunning = "Questionable.IsRunning";
    private const string IpcGetCurrentQuestId = "Questionable.GetCurrentQuestId";
    private const string IpcGetCurrentlyActiveEventQuests = "Questionable.GetCurrentlyActiveEventQuests";
    private const string IpcStartQuest = "Questionable.StartQuest";
    private const string IpcStartSingleQuest = "Questionable.StartSingleQuest";

    private readonly ICallGateProvider<bool> _isRunning;
    private readonly ICallGateProvider<string?> _getCurrentQuestId;
    private readonly ICallGateProvider<List<string>> _getCurrentlyActiveEventQuests;
    private readonly ICallGateProvider<string, bool> _startQuest;
    private readonly ICallGateProvider<string, bool> _startSingleQuest;

    public QuestionableIpc(
        QuestController questController,
        EventInfoComponent eventInfoComponent, QuestRegistry questRegistry,
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

        _startQuest = pluginInterface.GetIpcProvider<string, bool>(IpcStartQuest);
        _startQuest.RegisterFunc((string questId) => StartQuest(questController, questRegistry, questId, false));

        _startSingleQuest = pluginInterface.GetIpcProvider<string, bool>(IpcStartSingleQuest);
        _startSingleQuest.RegisterFunc((string questId) => StartQuest(questController, questRegistry, questId, true));
    }

    private static bool StartQuest(QuestController qc, QuestRegistry qr, string questId, bool single)
    {
        if (ElementId.TryFromString(questId, out var elementId) && elementId != null && qr.TryGetQuest(elementId, out var quest))
        {
            qc.SetNextQuest(quest);
            if (single)
                qc.StartSingleQuest("IPCQuestSelection");
            else
                qc.Start("IPCQuestSelection");
            return true;
        }
        return false;
    }

    public void Dispose()
    {
        _startQuest.UnregisterFunc();
        _getCurrentlyActiveEventQuests.UnregisterFunc();
        _getCurrentQuestId.UnregisterFunc();
        _isRunning.UnregisterFunc();
    }
}
