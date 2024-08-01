using System.Text.Json.Serialization;
using Questionable.Model.V1.Converter;

namespace Questionable.Model.V1;

[JsonConverter(typeof(QuestWorkModeConverter))]
public enum EQuestWorkMode
{
    Bitwise,
    Exact,
}
