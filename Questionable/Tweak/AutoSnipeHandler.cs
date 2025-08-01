using System;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Common.Lua;
using Questionable.Controller;

namespace Questionable.Tweak;

internal sealed unsafe class AutoSnipeHandler : IDisposable
{
    private readonly QuestController _questController;
    private readonly Hook<EnqueueSnipeTaskDelegate> _enqueueSnipeTaskHook;

    private delegate ulong EnqueueSnipeTaskDelegate(EventSceneModuleImplBase* scene, lua_State* state);

    public AutoSnipeHandler(QuestController questController, IGameInteropProvider gameInteropProvider)
    {
        _questController = questController;
        _enqueueSnipeTaskHook =
            gameInteropProvider.HookFromSignature<EnqueueSnipeTaskDelegate>(
               "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 48 8B F1 48 8D 4C 24 ?? E8 ?? ?? ?? ?? 48 8B 4C 24 ?? BA ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8D 4C 24",
                EnqueueSnipeTask);
    }

    public void Enable() => _enqueueSnipeTaskHook.Enable();

    private ulong EnqueueSnipeTask(EventSceneModuleImplBase* scene, lua_State* state)
    {
        if ( _questController.IsRunning)
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
        _enqueueSnipeTaskHook.Disable();
        _enqueueSnipeTaskHook.Dispose();
    }
}