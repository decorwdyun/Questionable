using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Questionable.Controller;
using Questionable.Data;
using Questionable.Model.Common;
using System;

namespace Questionable.External;

internal sealed class TextAdvanceIpc : IDisposable
{
    private bool _isExternalControlActivated;
    private readonly QuestController _questController;
    private readonly IFramework _framework;
    private readonly ICallGateSubscriber<bool> _isInExternalControl;
    private readonly ICallGateSubscriber<string, ExternalTerritoryConfig, bool> _enableExternalControl;
    private readonly ICallGateSubscriber<string, bool> _disableExternalControl;
    private readonly string _pluginName;
    private readonly ExternalTerritoryConfig _externalTerritoryConfig = new();

    public TextAdvanceIpc(IDalamudPluginInterface pluginInterface, IFramework framework, QuestController questController)
    {
        _framework = framework;
        _questController = questController;
        _isInExternalControl = pluginInterface.GetIpcSubscriber<bool>("TextAdvance.IsInExternalControl");
        _enableExternalControl = pluginInterface.GetIpcSubscriber<string, ExternalTerritoryConfig, bool>("TextAdvance.EnableExternalControl");
        _disableExternalControl = pluginInterface.GetIpcSubscriber<string, bool>("TextAdvance.DisableExternalControl");
        _pluginName = pluginInterface.InternalName;
        _framework.Update += OnUpdate;
    }

    public void Dispose()
    {
        _framework.Update -= OnUpdate;
        if(_isExternalControlActivated)
        {
            _disableExternalControl.InvokeFunc(_pluginName);
        }
    }

    public void OnUpdate(IFramework framework)
    {
        if(_questController.IsRunning)
        {
            if(!_isInExternalControl.InvokeFunc())
            {
                if(_enableExternalControl.InvokeFunc(_pluginName, _externalTerritoryConfig))
                {
                    _isExternalControlActivated = true;
                }
            }
        }
        else
        {
            if(_isExternalControlActivated)
            {
                if(_disableExternalControl.InvokeFunc(_pluginName) || !_isInExternalControl.InvokeFunc())
                {
                    _isExternalControlActivated = false;
                }
            }
        }
    }

    public class ExternalTerritoryConfig
    {
        public bool? EnableQuestAccept = true;
        public bool? EnableQuestComplete = true;
        public bool? EnableRewardPick = true;
        public bool? EnableRequestHandin = true;
        public bool? EnableCutsceneEsc = true;
        public bool? EnableCutsceneSkipConfirm = true;
        public bool? EnableTalkSkip = true;
        public bool? EnableRequestFill = true;
        public bool? EnableAutoInteract = false;
    }
}
