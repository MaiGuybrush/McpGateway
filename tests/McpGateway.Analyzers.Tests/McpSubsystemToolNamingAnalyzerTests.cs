using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace McpGateway.Analyzers.Tests;

public class McpSubsystemToolNamingAnalyzerTests
{
    private const string AttributeDeclarations = @"
namespace ModelContextProtocol
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class McpServerToolTypeAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class McpServerToolAttribute : System.Attribute
    {
        public string? Name { get; set; }
        public bool UseStructuredContent { get; set; }
        public McpServerToolAttribute() { }
        public McpServerToolAttribute(string name) { Name = name; }
    }
}
";

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(
        string source,
        string assemblyName = "McpGateway.Mfg.Mes")
    {
        var attrTree = CSharpSyntaxTree.ParseText(AttributeDeclarations);
        var sourceTree = CSharpSyntaxTree.ParseText(source);

        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { attrTree, sourceTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compileErrors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (compileErrors.Any())
        {
            throw new InvalidOperationException("Source compilation failed: " + string.Join("; ", compileErrors.Select(e => e.ToString())));
        }

        var analyzer = new McpSubsystemToolNamingAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();

        return diagnostics.Where(d => d.Id == McpSubsystemToolNamingAnalyzer.DiagnosticId).ToImmutableArray();
    }

    [Fact]
    public async Task ValidToolName_NamedArgument_NoDiagnosticEmitted()
    {
        var source = @"
using System.Threading.Tasks;
using ModelContextProtocol;

namespace McpGateway.Mfg.Mes;

[McpServerToolType]
public class MesLotQueryTool
{
    [McpServerTool(Name = ""mfg_mes_query_lot"", UseStructuredContent = true)]
    public Task<string> QueryLotAsync() => Task.FromResult(""ok"");
}
";
        var diagnostics = await RunAnalyzerAsync(source, "McpGateway.Mfg.Mes");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ValidToolName_ConstructorArgument_NoDiagnosticEmitted()
    {
        var source = @"
using System.Threading.Tasks;
using ModelContextProtocol;

namespace McpGateway.Mfg.Mes;

[McpServerToolType]
public class MesLotQueryTool
{
    [McpServerTool(""mfg_mes_query_lot"")]
    public Task<string> QueryLotAsync() => Task.FromResult(""ok"");
}
";
        var diagnostics = await RunAnalyzerAsync(source, "McpGateway.Mfg.Mes");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task NonSubsystemProject_TwoSegments_NoDiagnosticEmitted()
    {
        var source = @"
using System.Threading.Tasks;
using ModelContextProtocol;

namespace McpGateway.Report;

[McpServerToolType]
public class ReportYieldTool
{
    [McpServerTool(Name = ""report_query_daily_yield"", UseStructuredContent = true)]
    public Task<string> QueryYieldAsync() => Task.FromResult(""ok"");
}
";
        // Non-subsystem project (only 2 parts: McpGateway.Report)
        var diagnostics = await RunAnalyzerAsync(source, "McpGateway.Report");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task MissingDepartmentPrefix_EmitsMCP003()
    {
        var source = @"
using System.Threading.Tasks;
using ModelContextProtocol;

namespace McpGateway.Mfg.Mes;

[McpServerToolType]
public class MesLotQueryTool
{
    [McpServerTool(Name = ""mes_query_lot"", UseStructuredContent = true)]
    public Task<string> QueryLotAsync() => Task.FromResult(""ok"");
}
";
        var diagnostics = await RunAnalyzerAsync(source, "McpGateway.Mfg.Mes");
        Assert.Single(diagnostics);
        Assert.Equal(McpSubsystemToolNamingAnalyzer.DiagnosticId, diagnostics[0].Id);
        Assert.Contains("department prefix 'mes' does not match expected department 'mfg'", diagnostics[0].GetMessage());
    }

    [Fact]
    public async Task MismatchedSubsystem_EmitsMCP003()
    {
        var source = @"
using System.Threading.Tasks;
using ModelContextProtocol;

namespace McpGateway.Mfg.Mes;

[McpServerToolType]
public class MesLotQueryTool
{
    [McpServerTool(Name = ""mfg_wms_query_stock"", UseStructuredContent = true)]
    public Task<string> QueryStockAsync() => Task.FromResult(""ok"");
}
";
        var diagnostics = await RunAnalyzerAsync(source, "McpGateway.Mfg.Mes");
        Assert.Single(diagnostics);
        Assert.Equal(McpSubsystemToolNamingAnalyzer.DiagnosticId, diagnostics[0].Id);
        Assert.Contains("subsystem segment 'wms' does not match expected subsystem 'mes'", diagnostics[0].GetMessage());
    }

    [Fact]
    public async Task FewerThanThreeSegments_EmitsMCP003()
    {
        var source = @"
using System.Threading.Tasks;
using ModelContextProtocol;

namespace McpGateway.Mfg.Mes;

[McpServerToolType]
public class MesLotQueryTool
{
    [McpServerTool(Name = ""query_lot"", UseStructuredContent = true)]
    public Task<string> QueryLotAsync() => Task.FromResult(""ok"");
}
";
        var diagnostics = await RunAnalyzerAsync(source, "McpGateway.Mfg.Mes");
        Assert.Single(diagnostics);
        Assert.Equal(McpSubsystemToolNamingAnalyzer.DiagnosticId, diagnostics[0].Id);
        Assert.Contains("must contain at least 3 segments", diagnostics[0].GetMessage());
    }

    [Fact]
    public async Task UppercaseCharacters_EmitsMCP003()
    {
        var source = @"
using System.Threading.Tasks;
using ModelContextProtocol;

namespace McpGateway.Mfg.Mes;

[McpServerToolType]
public class MesLotQueryTool
{
    [McpServerTool(Name = ""mfg_mes_QueryLot"", UseStructuredContent = true)]
    public Task<string> QueryLotAsync() => Task.FromResult(""ok"");
}
";
        var diagnostics = await RunAnalyzerAsync(source, "McpGateway.Mfg.Mes");
        Assert.Single(diagnostics);
        Assert.Equal(McpSubsystemToolNamingAnalyzer.DiagnosticId, diagnostics[0].Id);
        Assert.Contains("lowercase ASCII letters, digits, and underscores", diagnostics[0].GetMessage());
    }

    [Fact]
    public async Task MissingExplicitName_EmitsMCP003()
    {
        var source = @"
using System.Threading.Tasks;
using ModelContextProtocol;

namespace McpGateway.Mfg.Mes;

[McpServerToolType]
public class MesLotQueryTool
{
    [McpServerTool]
    public Task<string> QueryLotAsync() => Task.FromResult(""ok"");
}
";
        var diagnostics = await RunAnalyzerAsync(source, "McpGateway.Mfg.Mes");
        Assert.Single(diagnostics);
        Assert.Equal(McpSubsystemToolNamingAnalyzer.DiagnosticId, diagnostics[0].Id);
        Assert.Contains("must explicitly specify a tool name", diagnostics[0].GetMessage());
    }

    [Fact]
    public async Task AddMcpSubsystemRegistration_WithInvalidTool_EmitsMCP003()
    {
        var source = @"
using System;
using System.Threading.Tasks;
using ModelContextProtocol;

namespace McpGateway.Mfg.Host
{
    public interface ISubsystemBuilder
    {
        ISubsystemBuilder WithTools<T>();
    }

    public static class SubsystemExtensions
    {
        public static void AddMcpSubsystem(this object services, string system, Action<ISubsystemBuilder> configure)
        {
            configure(null!);
        }
    }

    [McpServerToolType]
    public class BadTool
    {
        [McpServerTool(Name = ""query_lot"")]
        public Task<string> QueryLotAsync() => Task.FromResult(""ok"");
    }

    public class Startup
    {
        public void Configure(object services)
        {
            services.AddMcpSubsystem(""mes"", sub =>
            {
                sub.WithTools<BadTool>();
            });
        }
    }
}
";
        var diagnostics = await RunAnalyzerAsync(source, "McpGateway.Mfg.Host");
        Assert.Single(diagnostics);
        Assert.Equal(McpSubsystemToolNamingAnalyzer.DiagnosticId, diagnostics[0].Id);
    }

    [Fact]
    public async Task CodeFix_ReplacesLiteral_WithCorrectPrefix()
    {
        var source = @"
using System.Threading.Tasks;
using ModelContextProtocol;

namespace McpGateway.Mfg.Mes;

[McpServerToolType]
public class MesLotQueryTool
{
    [McpServerTool(Name = ""mes_query_lot"")]
    public Task<string> QueryLotAsync() => Task.FromResult(""ok"");
}
";
        var attrTree = CSharpSyntaxTree.ParseText(AttributeDeclarations);
        var sourceTree = CSharpSyntaxTree.ParseText(source);

        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            "McpGateway.Mfg.Mes",
            new[] { attrTree, sourceTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzer = new McpSubsystemToolNamingAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();

        Assert.Single(diagnostics);

        var project = new AdhocWorkspace().AddProject("McpGateway.Mfg.Mes", LanguageNames.CSharp);
        var document = project.AddDocument("Test.cs", sourceTree.GetText());

        var actions = new System.Collections.Generic.List<Microsoft.CodeAnalysis.CodeActions.CodeAction>();
        var context = new Microsoft.CodeAnalysis.CodeFixes.CodeFixContext(document, diagnostics[0], (a, _) => actions.Add(a), default);
        var provider = new McpSubsystemToolNamingCodeFixProvider();
        await provider.RegisterCodeFixesAsync(context);

        Assert.NotEmpty(actions);
        var operations = await actions[0].GetOperationsAsync(default);
        var solution = operations.OfType<Microsoft.CodeAnalysis.CodeActions.ApplyChangesOperation>().Single().ChangedSolution;
        var changedDoc = solution.GetDocument(document.Id);
        var changedText = (await changedDoc!.GetTextAsync()).ToString();

        Assert.Contains("\"mfg_mes_query_lot\"", changedText);
    }
}

