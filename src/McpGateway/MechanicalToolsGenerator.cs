using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Server;
using ModelContextProtocol.Protocol.Types;

namespace McpGateway;

public class MechanicalToolsGenerator
{
    private readonly string _openApiSpecPath;
    private readonly List<OperationInfo> _operations;
    private readonly List<ConvertedTool> _convertedTools;

    public MechanicalToolsGenerator(string openApiSpecPath)
    {
        _openApiSpecPath = openApiSpecPath;
        _operations = new List<OperationInfo>();
        _convertedTools = new List<ConvertedTool>();
    }

    public async Task<List<ConvertedTool>> GenerateToolsAsync()
    {
        Console.WriteLine($"Loading OpenAPI spec from: {_openApiSpecPath}");
        
        var document = OpenApiParser.LoadOpenApiDocument(_openApiSpecPath);
        if (document == null)
        {
            throw new InvalidOperationException("Failed to load OpenAPI document");
        }

        _operations.Clear();
        _operations.AddRange(OpenApiParser.ExtractOperations(document));

        Console.WriteLine($"Extracted {_operations.Count} operations");

        foreach (var operation in _operations)
        {
            var tool = ToolGenerator.ConvertToMcpTool(operation);
            var convertedTool = new ConvertedTool
            {
                Name = tool.Name,
                Tool = tool,
                Operation = operation
            };
            
            _convertedTools.Add(convertedTool);
            Console.WriteLine($"Generated tool: {tool.Name}");
        }

        return _convertedTools;
    }

    public List<ConvertedTool> GetConvertedTools()
    {
        return _convertedTools;
    }

    public ConvertedTool? GetToolByName(string name)
    {
        return _convertedTools.FirstOrDefault(t => t.Name == name);
    }

    public void ExportToolsToJson(string outputPath)
    {
        var toolsData = _convertedTools.Select(t => new
        {
            name = t.Name,
            description = t.Tool.Description,
            schema = t.Tool.InputSchema
        }).ToList();

        var json = System.Text.Json.JsonSerializer.Serialize(toolsData, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        System.IO.File.WriteAllText(outputPath, json);
        Console.WriteLine($"Exported tools to: {outputPath}");
    }
}

public class ConvertedTool
{
    public required string Name { get; set; }
    public required McpTool Tool { get; set; }
    public required OperationInfo Operation { get; set; }
}