using System.Text.Json;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

namespace McpGateway.Tools;

public interface IOpenApiParser
{
    Task<OpenApiDocument> ParseAsync(string filePath);
    IEnumerable<OpenApiOperationInfo> GetOperations(OpenApiDocument document);
}

public class OpenApiOperationInfo
{
    public string Path { get; set; } = string.Empty;
    public OperationType Method { get; set; }
    public OpenApiOperation Operation { get; set; } = new();
}

public class OpenApiParser : IOpenApiParser
{
    public async Task<OpenApiDocument> ParseAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"OpenAPI spec file not found: {filePath}");
        }

        var json = await File.ReadAllTextAsync(filePath);
        var reader = new OpenApiStringReader();
        
        var doc = reader.Read(json, out var diagnostic);
        
        if (diagnostic.Errors.Any())
        {
            var errors = string.Join(", ", diagnostic.Errors.Select(e => e.Message));
            throw new InvalidOperationException($"OpenAPI parse errors: {errors}");
        }
        
        return doc;
    }

    public IEnumerable<OpenApiOperationInfo> GetOperations(OpenApiDocument document)
    {
        var operations = new List<OpenApiOperationInfo>();
        
        foreach (var pathItem in document.Paths)
        {
            foreach (var operation in pathItem.Value.Operations)
            {
                operations.Add(new OpenApiOperationInfo
                {
                    Path = pathItem.Key,
                    Method = operation.Key,
                    Operation = operation.Value
                });
            }
        }
        
        return operations;
    }
}