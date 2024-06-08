using System.Text.Json.Serialization;
using JetBrains.Annotations;
using Questionable.Model.V1.Converter;

namespace Questionable.Model.V1;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
internal sealed class DialogueChoice
{
    [JsonConverter(typeof(DialogueChoiceTypeConverter))]
    public EDialogChoiceType Type { get; set; }
    public string? ExcelSheet { get; set; }
    public string? Prompt { get; set; }
    public bool Yes { get; set; } = true;
    public string? Answer { get; set; }
}
