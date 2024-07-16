using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Questionable.Model.V1;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Questionable.QuestPathGenerator;

public static class RoslynShortcuts
{
    public static IEnumerable<SyntaxNodeOrToken> SyntaxNodeList(params SyntaxNodeOrToken?[] nodes)
    {
        nodes = nodes.Where(x => x != null).ToArray();
        if (nodes.Length == 0)
            return [];

        List<SyntaxNodeOrToken> list = new();
        for (int i = 0; i < nodes.Length; ++i)
        {
            if (i > 0)
                list.Add(Token(SyntaxKind.CommaToken));
            list.Add(nodes[i].GetValueOrDefault());
        }

        return list;
    }

    public static ExpressionSyntax LiteralValue<T>(T? value)
    {
        if (value is string s)
            return LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(s));
        else if (value is bool b)
            return LiteralExpression(b ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression);
        else if (value is short i16)
            return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(i16));
        else if (value is int i32)
            return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(i32));
        else if (value is ushort u16)
            return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(u16));
        else if (value is uint u32)
            return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(u32));
        else if (value is float f)
            return LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(f));
        else if (value != null && value.GetType().IsEnum)
            return MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName(value.GetType().Name),
                IdentifierName(value.GetType().GetEnumName(value)!));
        else if (value is Vector3 vector)
        {
            return ObjectCreationExpression(
                    IdentifierName(nameof(Vector3)))
                .WithArgumentList(
                    ArgumentList(
                        SeparatedList<ArgumentSyntax>(
                            new SyntaxNodeOrToken[]
                            {
                                Argument(LiteralValue(vector.X)),
                                Token(SyntaxKind.CommaToken),
                                Argument(LiteralValue(vector.Y)),
                                Token(SyntaxKind.CommaToken),
                                Argument(LiteralValue(vector.Z))
                            })));
        }
        else if (value is AethernetShortcut aethernetShortcut)
        {
            return ObjectCreationExpression(
                    IdentifierName(nameof(AethernetShortcut)))
                .WithInitializer(
                    InitializerExpression(
                        SyntaxKind.ObjectInitializerExpression,
                        SeparatedList<ExpressionSyntax>(
                            SyntaxNodeList(
                                Assignment<EAetheryteLocation?>(nameof(AethernetShortcut.From), aethernetShortcut.From,
                                        null)
                                    .AsSyntaxNodeOrToken(),
                                Assignment<EAetheryteLocation?>(nameof(AethernetShortcut.To), aethernetShortcut.To,
                                        null)
                                    .AsSyntaxNodeOrToken()))));
        }
        else if (value is ChatMessage chatMessage)
        {
            ChatMessage emptyMessage = new();
            return ObjectCreationExpression(
                    IdentifierName(nameof(ChatMessage)))
                .WithInitializer(
                    InitializerExpression(
                        SyntaxKind.ObjectInitializerExpression,
                        SeparatedList<ExpressionSyntax>(
                            SyntaxNodeList(
                                Assignment(nameof(ChatMessage.ExcelSheet), chatMessage.ExcelSheet,
                                        emptyMessage.ExcelSheet)
                                    .AsSyntaxNodeOrToken(),
                                Assignment(nameof(ChatMessage.Key), chatMessage.Key,
                                        emptyMessage.Key)
                                    .AsSyntaxNodeOrToken()))));
        }
        else if (value is DialogueChoice dialogueChoice)
        {
            DialogueChoice emptyChoice = new();
            return ObjectCreationExpression(
                    IdentifierName(nameof(DialogueChoice)))
                .WithInitializer(
                    InitializerExpression(
                        SyntaxKind.ObjectInitializerExpression,
                        SeparatedList<ExpressionSyntax>(
                            SyntaxNodeList(
                                Assignment<EDialogChoiceType?>(nameof(DialogueChoice.Type), dialogueChoice.Type, null)
                                    .AsSyntaxNodeOrToken(),
                                Assignment(nameof(DialogueChoice.ExcelSheet), dialogueChoice.ExcelSheet,
                                        emptyChoice.ExcelSheet)
                                    .AsSyntaxNodeOrToken(),
                                Assignment(nameof(DialogueChoice.Prompt), dialogueChoice.Prompt, emptyChoice.Prompt)
                                    .AsSyntaxNodeOrToken(),
                                Assignment(nameof(DialogueChoice.Yes), dialogueChoice.Yes, emptyChoice.Yes)
                                    .AsSyntaxNodeOrToken(),
                                Assignment(nameof(DialogueChoice.Answer), dialogueChoice.Answer, emptyChoice.Answer)
                                    .AsSyntaxNodeOrToken(),
                                Assignment(nameof(DialogueChoice.DataId), dialogueChoice.DataId, emptyChoice.DataId)
                                    .AsSyntaxNodeOrToken()))));
        }
        else if (value is JumpDestination jumpDestination)
        {
            return ObjectCreationExpression(
                    IdentifierName(nameof(JumpDestination)))
                .WithInitializer(
                    InitializerExpression(
                        SyntaxKind.ObjectInitializerExpression,
                        SeparatedList<ExpressionSyntax>(
                            SyntaxNodeList(
                                Assignment<Vector3?>(nameof(JumpDestination.Position), jumpDestination.Position, null)
                                    .AsSyntaxNodeOrToken(),
                                Assignment(nameof(JumpDestination.StopDistance), jumpDestination.StopDistance, null)
                                    .AsSyntaxNodeOrToken(),
                                Assignment(nameof(JumpDestination.DelaySeconds), jumpDestination.DelaySeconds, null)
                                    .AsSyntaxNodeOrToken()))));
        }
        else if (value is ExcelRef excelRef)
        {
            if (excelRef.Type == ExcelRef.EType.Key)
            {
                return ObjectCreationExpression(
                        IdentifierName(nameof(ExcelRef)))
                    .WithArgumentList(
                        ArgumentList(
                            SingletonSeparatedList(
                                Argument(LiteralValue(excelRef.AsKey())))));
            }
            else if (excelRef.Type == ExcelRef.EType.RowId)
            {
                return ObjectCreationExpression(
                        IdentifierName(nameof(ExcelRef)))
                    .WithArgumentList(
                        ArgumentList(
                            SingletonSeparatedList(
                                Argument(LiteralValue(excelRef.AsRowId())))));
            }
            else
                throw new Exception($"Unsupported ExcelRef type {excelRef.Type}");
        }
        else if (value is ComplexCombatData complexCombatData)
        {
            return ObjectCreationExpression(
                    IdentifierName(nameof(ComplexCombatData)))
                .WithInitializer(
                    InitializerExpression(
                        SyntaxKind.ObjectInitializerExpression,
                        SeparatedList<ExpressionSyntax>(
                            SyntaxNodeList(
                                Assignment(nameof(ComplexCombatData.DataId), complexCombatData.DataId, default(uint))
                                    .AsSyntaxNodeOrToken(),
                                Assignment(nameof(ComplexCombatData.MinimumKillCount), complexCombatData.MinimumKillCount, null)
                                    .AsSyntaxNodeOrToken(),
                                Assignment(nameof(ComplexCombatData.RewardItemId), complexCombatData.RewardItemId, null)
                                    .AsSyntaxNodeOrToken(),
                                Assignment(nameof(ComplexCombatData.RewardItemCount), complexCombatData.RewardItemCount, null)
                                    .AsSyntaxNodeOrToken(),
                                AssignmentList(nameof(ComplexCombatData.CompletionQuestVariablesFlags), complexCombatData.CompletionQuestVariablesFlags)
                                    .AsSyntaxNodeOrToken()))));
        }else if (value is null)
            return LiteralExpression(SyntaxKind.NullLiteralExpression);
        else
            throw new Exception($"Unsupported data type {value.GetType()} = {value}");
    }

    public static AssignmentExpressionSyntax? Assignment<T>(string name, T? value, T? defaultValue)
    {
        if (value == null && defaultValue == null)
            return null;

        if (value != null && defaultValue != null && value.Equals(defaultValue))
            return null;

        return AssignmentExpression(
            SyntaxKind.SimpleAssignmentExpression,
            IdentifierName(name),
            LiteralValue(value));
    }

    public static AssignmentExpressionSyntax? AssignmentList<T>(string name, IEnumerable<T> value)
    {
        IEnumerable<T> list = value.ToList();
        if (!list.Any())
            return null;

        return AssignmentExpression(
            SyntaxKind.SimpleAssignmentExpression,
            IdentifierName(name),
            CollectionExpression(
                SeparatedList<CollectionElementSyntax>(
                    SyntaxNodeList(list.Select(x => ExpressionElement(
                        LiteralValue(x)).AsSyntaxNodeOrToken()).ToArray())
                )));
    }

    public static SyntaxNodeOrToken? AsSyntaxNodeOrToken(this SyntaxNode? node)
    {
        if (node == null)
            return null;

        return node;
    }
}
