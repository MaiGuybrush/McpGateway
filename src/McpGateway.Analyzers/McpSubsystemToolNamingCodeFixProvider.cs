using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace McpGateway.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(McpSubsystemToolNamingCodeFixProvider)), Shared]
public class McpSubsystemToolNamingCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(McpSubsystemToolNamingAnalyzer.DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;
        var node = root.FindNode(diagnosticSpan);

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel == null) return;

        // Try to determine department and subsystem
        var (dept, sys) = InferSubsystemFromDocument(context.Document, node, semanticModel);

        if (node is LiteralExpressionSyntax literalExpr && literalExpr.IsKind(SyntaxKind.StringLiteralExpression))
        {
            var currentValue = literalExpr.Token.ValueText;
            var suggestedName = BuildSuggestedName(currentValue, dept, sys);

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Change tool name to '{suggestedName}'",
                    createChangedDocument: c => ReplaceLiteralAsync(context.Document, literalExpr, suggestedName, c),
                    equivalenceKey: nameof(McpSubsystemToolNamingCodeFixProvider) + ".ReplaceLiteral"),
                diagnostic);
        }
        else if (node is AttributeSyntax attributeSyntax)
        {
            var methodDecl = attributeSyntax.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            var methodName = methodDecl?.Identifier.Text ?? "tool";
            var suggestedName = BuildSuggestedName(methodName, dept, sys);

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Add tool name Name = \"{suggestedName}\"",
                    createChangedDocument: c => AddNameArgumentAsync(context.Document, attributeSyntax, suggestedName, c),
                    equivalenceKey: nameof(McpSubsystemToolNamingCodeFixProvider) + ".AddName"),
                diagnostic);
        }
    }

    private static (string dept, string sys) InferSubsystemFromDocument(Document document, SyntaxNode node, SemanticModel semanticModel)
    {
        var dept = "dept";
        var sys = "sys";

        // 1. From Project / Assembly Name
        var projName = document.Project.Name;
        var parts = projName.Split('.');
        if (parts.Length >= 3 && parts[0].Equals("McpGateway", StringComparison.OrdinalIgnoreCase))
        {
            return (parts[1].ToLowerInvariant(), parts[2].ToLowerInvariant());
        }

        // 2. From Namespace
        var nsDecl = node.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>();
        if (nsDecl != null)
        {
            var nsParts = nsDecl.Name.ToString().Split('.');
            if (nsParts.Length >= 3 && nsParts[0].Equals("McpGateway", StringComparison.OrdinalIgnoreCase))
            {
                return (nsParts[1].ToLowerInvariant(), nsParts[2].ToLowerInvariant());
            }
        }

        return (dept, sys);
    }

    private static string BuildSuggestedName(string current, string dept, string sys)
    {
        var snake = ToSnakeCase(current.Trim('\"', ' '));
        var prefix = $"{dept}_{sys}_";

        if (snake.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return snake.ToLowerInvariant();
        }

        if (snake.StartsWith($"{sys}_", StringComparison.OrdinalIgnoreCase))
        {
            var rest = snake.Substring(sys.Length + 1);
            return $"{dept}_{sys}_{rest}".ToLowerInvariant();
        }

        if (snake.StartsWith($"{dept}_", StringComparison.OrdinalIgnoreCase))
        {
            var rest = snake.Substring(dept.Length + 1);
            if (!rest.StartsWith($"{sys}_", StringComparison.OrdinalIgnoreCase))
            {
                rest = $"{sys}_{rest}";
            }
            return $"{dept}_{rest}".ToLowerInvariant();
        }

        return $"{prefix}{snake}".ToLowerInvariant();
    }

    private static string ToSnakeCase(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var sb = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && text[i - 1] != '_' && !char.IsUpper(text[i - 1]))
                {
                    sb.Append('_');
                }
                sb.Append(char.ToLowerInvariant(c));
            }
            else if (c == '-' || c == ' ')
            {
                sb.Append('_');
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private static async Task<Document> ReplaceLiteralAsync(
        Document document,
        LiteralExpressionSyntax literalExpr,
        string newName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        var newLiteral = SyntaxFactory.LiteralExpression(
            SyntaxKind.StringLiteralExpression,
            SyntaxFactory.Literal(newName));

        var newRoot = root.ReplaceNode(literalExpr, newLiteral);
        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<Document> AddNameArgumentAsync(
        Document document,
        AttributeSyntax attributeSyntax,
        string newName,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null) return document;

        var nameArgument = SyntaxFactory.AttributeArgument(
            SyntaxFactory.NameEquals(SyntaxFactory.IdentifierName("Name")),
            null,
            SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(newName)));

        AttributeSyntax newAttribute;
        if (attributeSyntax.ArgumentList == null || attributeSyntax.ArgumentList.Arguments.Count == 0)
        {
            newAttribute = attributeSyntax.WithArgumentList(
                SyntaxFactory.AttributeArgumentList(SyntaxFactory.SingletonSeparatedList(nameArgument)));
        }
        else
        {
            var newArgs = attributeSyntax.ArgumentList.Arguments.Insert(0, nameArgument);
            newAttribute = attributeSyntax.WithArgumentList(
                attributeSyntax.ArgumentList.WithArguments(newArgs));
        }

        var newRoot = root.ReplaceNode(attributeSyntax, newAttribute);
        return document.WithSyntaxRoot(newRoot);
    }
}
