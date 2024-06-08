using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global",
    Justification = "Properties are used for serialization",
    Scope = "namespaceanddescendants",
    Target = "Questionable.Model.V1")]
[assembly: SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global",
    Justification = "Properties are used for serialization",
    Scope = "namespaceanddescendants",
    Target = "Questionable.Model.V1")]
