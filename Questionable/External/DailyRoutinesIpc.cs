using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;
using Questionable.Controller;

namespace Questionable.External;

internal sealed class DailyRoutinesIpc: IDisposable
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly ILogger<DailyRoutinesIpc> _logger;
    private readonly Configuration _configuration;
    private readonly IFramework _frameworkManager;
    private readonly QuestController _questController;


    private DateTime _nextCheckTime = DateTime.MinValue;
    private readonly string[] _conflictingModules =
    [
        "AutoTalkSkip",
        "AutoCutsceneSkip",
        "OptimizedInteraction",
        "AutoQuestComplete",
        "AutoRequestItemSubmit",
        "AutoFateSync"
    ];

    private HashSet<string> _suppressedModules = [];

    private readonly ICallGateSubscriber<string, bool?> _isModuleEnabled;
    private readonly ICallGateSubscriber<Version?> _dailyRoutinesVersion;
    private readonly ICallGateSubscriber<string, bool, bool> _loadModule;
    private readonly ICallGateSubscriber<string, bool, bool, bool> _unloadModule;

    private bool IsDailyRoutinesEnabled => _pluginInterface.InstalledPlugins.Any(x => x is { InternalName: "DailyRoutines", IsLoaded: true });

    public DailyRoutinesIpc(
        IDalamudPluginInterface pluginInterface,
        ILogger<DailyRoutinesIpc> logger,
        Configuration configuration,
        IFramework frameworkManager,
        QuestController questController
        )
    {
        _pluginInterface = pluginInterface;
        _logger = logger;
        _configuration = configuration;
        _frameworkManager = frameworkManager;
        _questController = questController;
        _isModuleEnabled = pluginInterface.GetIpcSubscriber<string, bool?>("DailyRoutines.IsModuleEnabled");
        _dailyRoutinesVersion = pluginInterface.GetIpcSubscriber<Version?>("DailyRoutines.Version");
        _loadModule = pluginInterface.GetIpcSubscriber<string, bool, bool>("DailyRoutines.LoadModule");
        _unloadModule = pluginInterface.GetIpcSubscriber<string, bool, bool, bool>("DailyRoutines.UnloadModule");
        
        _frameworkManager.Update += OnUpdate;
    }

    private void OnUpdate(IFramework framework)
    {
        if (IsDailyRoutinesEnabled == false) return;
    
        if (DateTime.Now < _nextCheckTime)
            return;
        _nextCheckTime = DateTime.Now.AddMilliseconds(500);
        
        var hasActiveQuest = _questController.IsRunning ||
                             _questController.AutomationType != QuestController.EAutomationType.Manual;
        try
        {
            if (_configuration.General.ConfigureDailyRoutines && hasActiveQuest)
            {

                var version = _dailyRoutinesVersion.InvokeFunc();
                if (version == null) return;


                foreach (var conflictingModule in _conflictingModules)
                {
                    var rt = _isModuleEnabled.InvokeFunc(conflictingModule);
                    if (rt.HasValue && rt.Value)
                    {
                        _unloadModule.InvokeFunc(conflictingModule, false, false);
                        _suppressedModules.Add(conflictingModule);
                        _logger.LogInformation("Suppressed conflicting module: {Module}", conflictingModule);
                    }
                }
            }
            else
            {
                RecoverSuppressedModules();
            }
        }
        catch
        {
            // ignored
        }
    }

    private void RecoverSuppressedModules()
    {
        if (IsDailyRoutinesEnabled && _suppressedModules.Count > 0)
        {
            foreach (var suppressedModule in _suppressedModules)
            {
                _loadModule.InvokeFunc(suppressedModule, false);
                _logger.LogInformation("Re-enabled suppressed module: {Module}", suppressedModule);
            }
            _suppressedModules = [];
        }
    }
    
    public void Dispose()
    {
        _frameworkManager.Update -= OnUpdate;
        RecoverSuppressedModules();
    }
}