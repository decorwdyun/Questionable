using System.Collections.Generic;
using System.Numerics;
using System.Text.Json.Serialization;
using Questionable.Model.V1.Converter;

namespace Questionable.Model.V1;

public class QuestStep
{
    [JsonConverter(typeof(InteractionTypeConverter))]
    public EInteractionType InteractionType { get; set; }

    public uint? DataId { get; set; }

    [JsonConverter(typeof(VectorConverter))]
    public Vector3? Position { get; set; }

    public float? StopDistance { get; set; }
    public ushort TerritoryId { get; set; }
    public ushort? TargetTerritoryId { get; set; }

    public bool Disabled { get; set; }
    public bool DisableNavmesh { get; set; }
    public bool? Mount { get; set; }
    public bool Fly { get; set; }
    public string? Comment { get; set; }

    [JsonConverter(typeof(AetheryteConverter))]
    public EAetheryteLocation? AetheryteShortcut { get; set; }

    [JsonConverter(typeof(AethernetShortcutConverter))]
    public AethernetShortcut? AethernetShortcut { get; set; }
    public uint? AetherCurrentId { get; set; }

    public uint? ItemId { get; set; }

    [JsonConverter(typeof(EmoteConverter))]
    public EEmote? Emote { get; set; }

    [JsonConverter(typeof(EnemySpawnTypeConverter))]
    public EEnemySpawnType? EnemySpawnType { get; set; }

    public IList<uint>? KillEnemyDataIds { get; set; }
}
