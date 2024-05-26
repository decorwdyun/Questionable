using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Serialization;
using Questionable.Model.V1.Converter;

namespace Questionable.Model.V1;

public class QuestStep
{
    [JsonConverter(typeof(InteractionTypeConverter))]
    public EInteractionType InteractionType { get; set; }

    public ulong? DataId { get; set; }
    public Vector3 Position { get; set; }
    public ushort TerritoryId { get; set; }
    public bool Disabled { get; set; }

    [JsonConverter(typeof(AethernetShortcutConverter))]
    public AethernetShortcut AethernetShortcut { get; set; }
}
