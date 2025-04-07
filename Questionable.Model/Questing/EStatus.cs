using System.Text.Json.Serialization;
using Questionable.Model.Questing.Converter;

namespace Questionable.Model.Questing;

[JsonConverter(typeof(StatusConverter))]
public enum EStatus : uint
{
    GatheringRateUp = 218,
    Hidden = 614,
    Eukrasia = 2606,
    Jog = 4209,
}
