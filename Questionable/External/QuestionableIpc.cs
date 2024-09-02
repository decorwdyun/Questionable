using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Questionable.Controller;

namespace Questionable.External;

internal sealed class QuestionableIpc : IDisposable
{
    private const string IpcIsRunning = "Questionable.IsRunning";
    private const string IpcGetCurrentQuestId = "Questionable.GetCurrentQuestId";

    private readonly ICallGateProvider<bool> _isRunning;
    private readonly ICallGateProvider<string?> _getCurrentQuestId;

    public QuestionableIpc(QuestController questController, IDalamudPluginInterface pluginInterface)
    {
        _isRunning = pluginInterface.GetIpcProvider<bool>(IpcIsRunning);
        _isRunning.RegisterFunc(() => questController.IsRunning);

        _getCurrentQuestId = pluginInterface.GetIpcProvider<string?>(IpcGetCurrentQuestId);
        _getCurrentQuestId.RegisterFunc(() => questController.CurrentQuest?.Quest.Id.ToString());
    }


    public void Dispose()
    {
        _getCurrentQuestId.UnregisterFunc();
        _isRunning.UnregisterFunc();
    }
}
