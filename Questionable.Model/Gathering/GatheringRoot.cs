using System.Collections.Generic;
using System.Text.Json.Serialization;
using Questionable.Model.Common;
using Questionable.Model.Common.Converter;
using Questionable.Model.Questing;
using Questionable.Model.Questing.Converter;

namespace Questionable.Model.Gathering;

public sealed class GatheringRoot
{
    [JsonConverter(typeof(StringListOrValueConverter))]
    public List<string> Author { get; set; } = [];

    public List<QuestStep> Steps { get; set; } = [];
    public bool? FlyBetweenNodes { get; set; }
    public List<uint> ExtraQuestItems { get; set; } = [];
    public List<GatheringNodeGroup> Groups { get; set; } = [];
}
