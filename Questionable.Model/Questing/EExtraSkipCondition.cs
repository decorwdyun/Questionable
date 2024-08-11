using System.Text.Json.Serialization;
using Questionable.Model.Questing.Converter;

namespace Questionable.Model.Questing;

[JsonConverter(typeof(SkipConditionConverter))]
public enum EExtraSkipCondition
{
    None,
    WakingSandsMainArea,

    RisingStonesSolar,
}
