using System.Collections.Generic;
using System.Text.Json.Serialization;
using Questionable.Model.Common;
using Questionable.Model.Common.Converter;
using Questionable.Model.Questing.Converter;

namespace Questionable.Model.Gathering;

public sealed class GatheringRoot
{
    [JsonConverter(typeof(StringListOrValueConverter))]
    public List<string> Author { get; set; } = [];
    public ushort TerritoryId { get; set; }

    [JsonConverter(typeof(AetheryteConverter))]
    public EAetheryteLocation? AetheryteShortcut { get; set; }

    public AethernetShortcut? AethernetShortcut { get; set; }
    public List<GatheringNodeGroup> Groups { get; set; } = [];
}
