using System.Text.Json.Serialization;
using Questionable.Model.V1.Converter;

namespace Questionable.Model.V1;

[JsonConverter(typeof(LockedSkipConditionConverter))]
public enum ELockedSkipCondition
{
    Locked,
    Unlocked,
}
