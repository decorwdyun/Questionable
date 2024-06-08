using System.Text.Json.Serialization;
using Questionable.Model.V1.Converter;

namespace Questionable.Model.V1;

[JsonConverter(typeof(AethernetShortcutConverter))]
internal sealed class AethernetShortcut
{
    public EAetheryteLocation From { get; set; }
    public EAetheryteLocation To { get; set; }
}
