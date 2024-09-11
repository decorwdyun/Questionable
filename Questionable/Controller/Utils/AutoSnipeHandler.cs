using System;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Common.Lua;

namespace Questionable.Controller.Utils;

internal sealed unsafe class AutoSnipeHandler : IDisposable
{
    private readonly QuestController _questController;
    private readonly Configuration _configuration;
    private readonly Hook<EnqueueSnipeTaskDelegate> _enqueueSnipeTaskHook;

    private delegate ulong EnqueueSnipeTaskDelegate(EventSceneModuleImplBase* scene, lua_State* state);

    public AutoSnipeHandler(QuestController questController, Configuration configuration, IGameInteropProvider gameInteropProvider)
    {
        _questController = questController;
        _configuration = configuration;

        _enqueueSnipeTaskHook =
            gameInteropProvider.HookFromSignature<EnqueueSnipeTaskDelegate>(
                "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 50 48 8B F1 48 8D 4C 24 ?? E8 ?? ?? ?? ?? 48 8B 4C 24 ??",
                EnqueueSnipeTask);
    }

    public void Enable() => _enqueueSnipeTaskHook.Enable();

    private ulong EnqueueSnipeTask(EventSceneModuleImplBase* scene, lua_State* state)
    {
        if (_configuration.General.AutomaticallyCompleteSnipeTasks && _questController.IsRunning)
        {
            var val = state->top;
            val->tt = 3;
            val->value.n = 1;
            state->top += 1;
            return 1;
        }
        else
            return _enqueueSnipeTaskHook.Original.Invoke(scene, state);
    }

    public void Dispose()
    {
        _enqueueSnipeTaskHook.Dispose();
    }
}
