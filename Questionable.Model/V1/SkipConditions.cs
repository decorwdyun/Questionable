namespace Questionable.Model.V1;

public sealed class SkipConditions
{
    public SkipStepConditions? StepIf { get; set; }
    public SkipAetheryteCondition? AetheryteShortcutIf { get; set; }
    public SkipAetheryteCondition? AethernetShortcutIf { get; set; }
}
