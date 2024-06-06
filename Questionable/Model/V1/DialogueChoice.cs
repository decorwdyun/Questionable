using System.Text.Json.Serialization;
using Questionable.Model.V1.Converter;

namespace Questionable.Model.V1;

public class DialogueChoice
{
    [JsonConverter(typeof(DialogueChoiceTypeConverter))]
    public EDialogChoiceType Type { get; set; }
    public string? ExcelSheet { get; set; }
    public string? Prompt { get; set; } = null!;
    public bool Yes { get; set; } = true;
    public string? Answer { get; set; }
}
