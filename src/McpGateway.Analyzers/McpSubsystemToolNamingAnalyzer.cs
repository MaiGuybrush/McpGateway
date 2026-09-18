using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace McpGateway.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class McpSubsystemToolNamingAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MCP003";

    private static readonly HashSet<string> ExcludedSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host",
        "Core",
        "Analyzers",
        "Tests",
        "IntegrationTests",
        "E2ETests",
        "Shared",
        "Common",
        "Specs",
        "Mock"
    };

    private static readonly Regex AllowedCharsRegex = new("^[a-z0-9_]+$", RegexOptions.Compiled);

    public static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Subsystem MCP Tool name must follow '{department}_{system}_{action}' convention",
        "Subsystem MCP Tool '{0}' has invalid tool name '{1}': {2}",
        "Naming",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Subsystem MCP tools must strictly follow the '{department}_{system}_{action}' three-segment naming convention to ensure endpoint routing and prevent name collisions in the global gateway registry.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // 1. Check tool methods directly (e.g. within subsystem project)
        context.RegisterSymbolAction(AnalyzeMethodSymbol, SymbolKind.Method);

        // 2. Check AddMcpSubsystem("sys", ...) registration calls
        context.RegisterSyntaxNodeAction(AnalyzeInvocationSyntax, SyntaxKind.InvocationExpression);
    }

    private void AnalyzeMethodSymbol(SymbolAnalysisContext context)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;

        // Determine if method is in a subsystem project or class
        if (!TryGetSubsystemContext(methodSymbol, context.Options, out var department, out var subsystem))
        {
            return;
        }

        // Check if method is an MCP Tool method
        var toolAttr = FindToolAttribute(methodSymbol);
        if (toolAttr == null)
        {
            return;
        }

        ValidateToolNaming(
            context.ReportDiagnostic,
            methodSymbol,
            toolAttr,
            department,
            subsystem);
    }

    private void AnalyzeInvocationSyntax(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return;

        // Look for AddMcpSubsystem("subsystemName", subsystem => { subsystem.WithTools<TTool>(); })
        if (memberAccess.Name.Identifier.Text != "AddMcpSubsystem")
            return;

        var args = invocation.ArgumentList?.Arguments;
        if (args == null || args.Value.Count < 1)
            return;

        // Extract subsystem name from first argument: AddMcpSubsystem("mes", ...)
        var firstArgExpr = args.Value[0].Expression;
        var constantValue = context.SemanticModel.GetConstantValue(firstArgExpr);
        if (!constantValue.HasValue || constantValue.Value is not string subsystemName || string.IsNullOrWhiteSpace(subsystemName))
            return;

        subsystemName = subsystemName.Trim().ToLowerInvariant();

        // Try to determine department from compilation / containing type
        TryGetDepartment(context.SemanticModel.Compilation, invocation, out var department);

        // Scan invocations inside the arguments for WithTools<T>()
        var withToolsInvocations = invocation.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv =>
            {
                if (inv.Expression is MemberAccessExpressionSyntax ma)
                {
                    return ma.Name.Identifier.Text == "WithTools";
                }
                return false;
            });

        foreach (var withToolsInv in withToolsInvocations)
        {
            if (withToolsInv.Expression is not MemberAccessExpressionSyntax ma ||
                ma.Name is not GenericNameSyntax genericName ||
                genericName.TypeArgumentList.Arguments.Count == 0)
            {
                continue;
            }

            var typeArgSyntax = genericName.TypeArgumentList.Arguments[0];
            var typeSymbol = context.SemanticModel.GetTypeInfo(typeArgSyntax).Type as INamedTypeSymbol;
            if (typeSymbol == null)
                continue;

            // Check all tool methods in TTool for compliance with this subsystem
            foreach (var member in typeSymbol.GetMembers().OfType<IMethodSymbol>())
            {
                var toolAttr = FindToolAttribute(member);
                if (toolAttr == null)
                    continue;

                ValidateToolNaming(
                    diagnostic => context.ReportDiagnostic(diagnostic),
                    member,
                    toolAttr,
                    department,
                    subsystemName,
                    overrideLocation: typeArgSyntax.GetLocation());
            }
        }
    }

    private static void ValidateToolNaming(
        Action<Diagnostic> reportDiagnostic,
        IMethodSymbol methodSymbol,
        AttributeData toolAttr,
        string? department,
        string? subsystem,
        Location? overrideLocation = null)
    {
        var (extractedToolName, location) = ExtractToolNameAndLocation(methodSymbol, toolAttr);
        var targetLocation = overrideLocation ?? location ?? methodSymbol.Locations.FirstOrDefault() ?? Location.None;

        // Case 1: Missing explicit tool name
        if (string.IsNullOrWhiteSpace(extractedToolName))
        {
            var expectedPrefix = BuildExpectedPrefix(department, subsystem);
            var reason = $"Subsystem MCP tool methods must explicitly specify a tool name conforming to '{expectedPrefix}{{action}}'";
            reportDiagnostic(Diagnostic.Create(Rule, targetLocation, methodSymbol.Name, "(unspecified)", reason));
            return;
        }

        var toolName = extractedToolName!;

        // Case 2: Invalid characters / casing
        if (!AllowedCharsRegex.IsMatch(toolName))
        {
            var reason = $"Tool name must contain only lowercase ASCII letters, digits, and underscores without uppercase or symbols (got '{toolName}')";
            reportDiagnostic(Diagnostic.Create(Rule, targetLocation, methodSymbol.Name, toolName, reason));
            return;
        }

        // Case 3: Trailing underscore
        if (toolName.EndsWith("_", StringComparison.Ordinal))
        {
            var reason = $"Tool name cannot end with an underscore (got '{toolName}')";
            reportDiagnostic(Diagnostic.Create(Rule, targetLocation, methodSymbol.Name, toolName, reason));
            return;
        }

        // Case 4: Segments check (must be at least {dept}_{system}_{action})
        var segments = toolName.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 3)
        {
            var expectedPrefix = BuildExpectedPrefix(department, subsystem);
            var reason = $"Tool name must contain at least 3 segments '{expectedPrefix}{{action}}' (got {segments.Length} segment(s): '{toolName}')";
            reportDiagnostic(Diagnostic.Create(Rule, targetLocation, methodSymbol.Name, toolName, reason));
            return;
        }

        // Case 5: Department check if department is known
        if (!string.IsNullOrEmpty(department))
        {
            var normDept = department!.ToLowerInvariant();
            if (!segments[0].Equals(normDept, StringComparison.OrdinalIgnoreCase))
            {
                var normSys = !string.IsNullOrEmpty(subsystem) ? subsystem!.ToLowerInvariant() : "{system}";
                var reason = $"Tool name department prefix '{segments[0]}' does not match expected department '{normDept}' (expected prefix '{normDept}_{normSys}_')";
                reportDiagnostic(Diagnostic.Create(Rule, targetLocation, methodSymbol.Name, toolName, reason));
                return;
            }
        }

        // Case 6: Subsystem check if subsystem is known
        if (!string.IsNullOrEmpty(subsystem))
        {
            var normSys = subsystem!.ToLowerInvariant();
            if (!segments[1].Equals(normSys, StringComparison.OrdinalIgnoreCase))
            {
                var expectedPrefix = BuildExpectedPrefix(department, normSys);
                var reason = $"Tool name subsystem segment '{segments[1]}' does not match expected subsystem '{normSys}' (expected prefix '{expectedPrefix}')";
                reportDiagnostic(Diagnostic.Create(Rule, targetLocation, methodSymbol.Name, toolName, reason));
                return;
            }
        }
    }

    private static string BuildExpectedPrefix(string? department, string? subsystem)
    {
        var d = !string.IsNullOrEmpty(department) ? department!.ToLowerInvariant() : "{department}";
        var s = !string.IsNullOrEmpty(subsystem) ? subsystem!.ToLowerInvariant() : "{system}";
        return $"{d}_{s}_";
    }

    private static (string? ToolName, Location? Location) ExtractToolNameAndLocation(
        IMethodSymbol methodSymbol,
        AttributeData toolAttr)
    {
        // Check constructor argument: [McpServerTool("tool_name")] or [McpTool("tool_name")]
        if (toolAttr.ConstructorArguments.Length > 0 &&
            toolAttr.ConstructorArguments[0].Value is string ctorName &&
            !string.IsNullOrWhiteSpace(ctorName))
        {
            var syntax = toolAttr.ApplicationSyntaxReference?.GetSyntax() as AttributeSyntax;
            var arg = syntax?.ArgumentList?.Arguments.FirstOrDefault(a => a.NameEquals == null);
            var loc = arg?.Expression.GetLocation() ?? arg?.GetLocation() ?? syntax?.GetLocation();
            return (ctorName, loc ?? methodSymbol.Locations.FirstOrDefault());
        }

        // Check named argument: [McpServerTool(Name = "tool_name")] or [McpTool(Name = "tool_name")]
        var namedArg = toolAttr.NamedArguments.FirstOrDefault(kv => kv.Key == "Name");
        if (namedArg.Value.Value is string namedName && !string.IsNullOrWhiteSpace(namedName))
        {
            var syntax = toolAttr.ApplicationSyntaxReference?.GetSyntax() as AttributeSyntax;
            var arg = syntax?.ArgumentList?.Arguments.FirstOrDefault(a => a.NameEquals?.Name.Identifier.Text == "Name");
            var loc = arg?.Expression.GetLocation() ?? arg?.GetLocation() ?? syntax?.GetLocation();
            return (namedName, loc ?? methodSymbol.Locations.FirstOrDefault());
        }

        var attrSyntax = toolAttr.ApplicationSyntaxReference?.GetSyntax();
        return (null, attrSyntax?.GetLocation() ?? methodSymbol.Locations.FirstOrDefault());
    }

    private static AttributeData? FindToolAttribute(IMethodSymbol methodSymbol)
    {
        return methodSymbol.GetAttributes().FirstOrDefault(a =>
            a.AttributeClass?.Name is "McpServerToolAttribute" or "McpServerTool" or "McpToolAttribute" or "McpTool");
    }

    private static bool TryGetSubsystemContext(
        ISymbol symbol,
        AnalyzerOptions options,
        out string? department,
        out string? subsystem)
    {
        department = null;
        subsystem = null;

        // 1. Try MSBuild properties
        if (options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.McpSubsystem", out var sysProp) &&
            !string.IsNullOrWhiteSpace(sysProp))
        {
            subsystem = sysProp.Trim().ToLowerInvariant();
            if (options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.McpDepartment", out var deptProp) &&
                !string.IsNullOrWhiteSpace(deptProp))
            {
                department = deptProp.Trim().ToLowerInvariant();
            }
            return true;
        }

        // 2. Try Assembly Name: McpGateway.<Department>.<System>
        var assemblyName = symbol.ContainingAssembly?.Name;
        if (!string.IsNullOrEmpty(assemblyName))
        {
            var parts = assemblyName!.Split(new[] { '.' }, StringSplitOptions.None);
            if (parts.Length >= 3 && parts[0].Equals("McpGateway", StringComparison.OrdinalIgnoreCase))
            {
                var candidateSys = parts[2];
                if (!ExcludedSegments.Contains(candidateSys))
                {
                    department = parts[1].ToLowerInvariant();
                    subsystem = candidateSys.ToLowerInvariant();
                    return true;
                }
            }
        }

        // 3. Try Containing Namespace: McpGateway.<Department>.<System>...
        var ns = symbol.ContainingType?.ContainingNamespace?.ToDisplayString();
        if (!string.IsNullOrEmpty(ns))
        {
            var nsParts = ns!.Split(new[] { '.' }, StringSplitOptions.None);
            if (nsParts.Length >= 3 && nsParts[0].Equals("McpGateway", StringComparison.OrdinalIgnoreCase))
            {
                var candidateSys = nsParts[2];
                if (!ExcludedSegments.Contains(candidateSys))
                {
                    department = nsParts[1].ToLowerInvariant();
                    subsystem = candidateSys.ToLowerInvariant();
                    return true;
                }
            }
        }

        // 4. Try Attributes: [McpSubsystem("mes")] or [McpSubsystem("mfg", "mes")] on Class, Method, or Assembly
        var attributes = symbol.GetAttributes()
            .Concat(symbol.ContainingType?.GetAttributes() ?? Enumerable.Empty<AttributeData>())
            .Concat(symbol.ContainingAssembly?.GetAttributes() ?? Enumerable.Empty<AttributeData>());

        foreach (var attr in attributes)
        {
            var attrName = attr.AttributeClass?.Name;
            if (attrName is "McpSubsystemAttribute" or "McpSubsystem" or "SubsystemAttribute" or "Subsystem")
            {
                if (attr.ConstructorArguments.Length == 1 &&
                    attr.ConstructorArguments[0].Value is string sys &&
                    !string.IsNullOrWhiteSpace(sys))
                {
                    subsystem = sys.Trim().ToLowerInvariant();
                    return true;
                }
                else if (attr.ConstructorArguments.Length >= 2 &&
                         attr.ConstructorArguments[0].Value is string d &&
                         attr.ConstructorArguments[1].Value is string s)
                {
                    department = d.Trim().ToLowerInvariant();
                    subsystem = s.Trim().ToLowerInvariant();
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryGetDepartment(Compilation compilation, SyntaxNode node, out string? department)
    {
        department = null;

        var assemblyName = compilation.AssemblyName;
        if (!string.IsNullOrEmpty(assemblyName))
        {
            var parts = assemblyName!.Split(new[] { '.' }, StringSplitOptions.None);
            if (parts.Length >= 2 && parts[0].Equals("McpGateway", StringComparison.OrdinalIgnoreCase))
            {
                department = parts[1].ToLowerInvariant();
                return true;
            }
        }

        return false;
    }
}
