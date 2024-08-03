using System.Collections.Generic;
using System.Text.Json.Serialization;
using Questionable.Model.Common;
using Questionable.Model.Common.Converter;

namespace Questionable.Model.Gathering;

public sealed class GatheringRoot
{
    [JsonConverter(typeof(StringListOrValueConverter))]
    public List<string> Author { get; set; } = [];
    public ushort TerritoryId { get; set; }

    [JsonConverter(typeof(AetheryteConverter))]
    public EAetheryteLocation? AetheryteShortcut { get; set; }

    public List<GatheringNodeGroup> Groups { get; set; } = [];
}
