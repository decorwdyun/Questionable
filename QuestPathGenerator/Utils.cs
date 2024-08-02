using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Json.Schema;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Questionable.QuestPathGenerator;

public static class Utils
{
    public static IEnumerable<(ushort, JsonNode)> GetAdditionalFiles(GeneratorExecutionContext context,
        AdditionalText jsonSchemaFile, JsonSchema jsonSchema, DiagnosticDescriptor invalidJson)
    {
        var commonSchemaFile = context.AdditionalFiles.Single(x => Path.GetFileName(x.Path) == "common-schema.json");
        List<AdditionalText> jsonSchemaFiles = [jsonSchemaFile, commonSchemaFile];

        SchemaRegistry.Global.Register(
            new Uri("https://git.carvel.li/liza/Questionable/raw/branch/master/Questionable.Model/common-schema.json"),
            JsonSchema.FromText(commonSchemaFile.GetText()!.ToString()));

        foreach (var additionalFile in context.AdditionalFiles)
        {
            if (additionalFile == null || jsonSchemaFiles.Contains(additionalFile))
                continue;

            if (Path.GetExtension(additionalFile.Path) != ".json")
                continue;

            string name = Path.GetFileName(additionalFile.Path);
            if (!name.Contains("_"))
                continue;

            ushort id = ushort.Parse(name.Substring(0, name.IndexOf('_')));

            var text = additionalFile.GetText();
            if (text == null)
                continue;

            var node = JsonNode.Parse(text.ToString());
            if (node == null)
                continue;

            string? schemaLocation = node["$schema"]?.GetValue<string?>();
            if (schemaLocation == null || new Uri(schemaLocation) != jsonSchema.GetId())
                continue;

            var evaluationResult = jsonSchema.Evaluate(node, new EvaluationOptions
            {
                Culture = CultureInfo.InvariantCulture,
                OutputFormat = OutputFormat.List,
            });
            if (!evaluationResult.IsValid)
            {
                var error = Diagnostic.Create(invalidJson,
                    null,
                    Path.GetFileName(additionalFile.Path));
                context.ReportDiagnostic(error);
            }

            yield return (id, node);
        }
    }

    public static List<MethodDeclarationSyntax> CreateMethods<T>(string prefix,
        List<IGrouping<string, (ushort, T)>> partitions,
        Func<List<(ushort, T)>, StatementSyntax[]> toInitializers)
    {
        List<MethodDeclarationSyntax> methods =
        [
            MethodDeclaration(
                    PredefinedType(
                        Token(SyntaxKind.VoidKeyword)),
                    Identifier(prefix))
                .WithModifiers(
                    TokenList(
                        Token(SyntaxKind.PrivateKeyword),
                        Token(SyntaxKind.StaticKeyword)))
                .WithBody(
                    Block(
                        partitions
                            .Select(x =>
                                ExpressionStatement(
                                    InvocationExpression(
                                        IdentifierName(x.Key))))))
        ];

        foreach (var partition in partitions)
        {
            methods.Add(MethodDeclaration(
                    PredefinedType(
                        Token(SyntaxKind.VoidKeyword)),
                    Identifier(partition.Key))
                .WithModifiers(
                    TokenList(
                        Token(SyntaxKind.PrivateKeyword),
                        Token(SyntaxKind.StaticKeyword)))
                .WithBody(
                    Block(toInitializers(partition.ToList()))));
        }

        return methods;
    }
}
