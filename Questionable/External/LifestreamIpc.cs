using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Questionable.Data;
using Questionable.Model.Common;

namespace Questionable.External;

internal sealed class LifestreamIpc
{
    private readonly AetheryteData _aetheryteData;
    private readonly ICallGateSubscriber<string, bool> _aethernetTeleport;

    public LifestreamIpc(IDalamudPluginInterface pluginInterface, AetheryteData aetheryteData)
    {
        _aetheryteData = aetheryteData;
        _aethernetTeleport = pluginInterface.GetIpcSubscriber<string, bool>("Lifestream.AethernetTeleport");
    }

    public bool Teleport(EAetheryteLocation aetheryteLocation)
    {
        if (aetheryteLocation == EAetheryteLocation.IshgardFirmament)
        {
            // TODO does this even work on non-EN clients?
            return _aethernetTeleport.InvokeFunc("Firmament");
        }

        if (!_aetheryteData.AethernetNames.TryGetValue(aetheryteLocation, out string? name))
            return false;

        return _aethernetTeleport.InvokeFunc(name);
    }
}
