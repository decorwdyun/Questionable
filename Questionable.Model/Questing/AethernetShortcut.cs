using System.Text.Json.Serialization;
using Questionable.Model.Common;
using Questionable.Model.Questing.Converter;

namespace Questionable.Model.Questing;

[JsonConverter(typeof(AethernetShortcutConverter))]
public sealed class AethernetShortcut
{
    public EAetheryteLocation From { get; set; }
    public EAetheryteLocation To { get; set; }
}
