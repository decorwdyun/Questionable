using JetBrains.Annotations;

namespace Questionable.Model.V1;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
internal sealed class ChatMessage
{
    public string? ExcelSheet { get; set; }
    public string Key { get; set; } = null!;
}
