using System.IO;

namespace Questionable.Model;

public static class AssemblyModelLoader
{
    public static Stream CommonSchema =>
        typeof(AssemblyModelLoader).Assembly.GetManifestResourceStream("Questionable.Model.CommonSchema")!;
}
