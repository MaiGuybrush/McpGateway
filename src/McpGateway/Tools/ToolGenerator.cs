using System;
using System.Collections.Generic;
using System.Text.Json;
using ModelContextProtocol.Protocol.Types;
using Mcp = ModelContextProtocol;

namespace McpGateway.Tools;

public class ToolGenerator
{
    public static McpTool ConvertToMcpTool(OperationInfo operation)
    {
        return new McpTool
        {
            Name = GenerateToolName(operation),
            Description = GenerateToolDescription(operation),
            InputSchema = GenerateInputSchema(operation)
        };
    }
    
    private static string GenerateToolName(OperationInfo operation)
    {
        // Convert OperationId to camelCase tool name
        var operationId = operation.OperationId;
        if (string.IsNullOrEmpty(operationId))
        {
            operationId = $"{operation.HttpMethod.ToLowerInvariant()}_{operation.Path.Replace("/", "_")}";
        }
        
        // Clean up the name
        operationId = operationId.Replace("-", "_").Replace("{", "").Replace("}", "");
        
        // Convert to PascalCase for the tool name
        return ToPascalCase(operationId);
    }
    
    private static string GenerateToolDescription(OperationInfo operation)
    {
        var description = operation.Description;
        if (string.IsNullOrEmpty(description))
        {
            description = operation.Summary;
        }
        
        if (string.IsNullOrEmpty(description))
        {
            description = $"{operation.HttpMethod} request to {operation.Path}";
        }
        
        return description;
    }
    
    private static ToolInputSchema GenerateInputSchema(OperationInfo operation)
    {
        var properties = new Dictionary<string, JsonElement>();
        var required = new List<string>();
        
        // Add parameters to schema
        foreach (var param in operation.Parameters)
        {
            var paramSchema = new Dictionary<string, object>();
            
            // Determine JSON Schema type
            switch (param.Type.ToLower())
            {
                case "integer":
                    paramSchema["type"] = "integer";
                    break;
                case "number":
                    paramSchema["type"] = "number";
                    break;
                case "boolean":
                    paramSchema["type"] = "boolean";
                    break;
                case "array":
                    paramSchema["type"] = "array";
                    break;
                case "object":
                    paramSchema["type"] = "object";
                    break;
                default:
                    paramSchema["type"] = "string";
                    break;
            }
            
            // Add description
            if (!string.IsNullOrEmpty(param.Description))
            {
                paramSchema["description"] = param.Description;
            }
            
            // Add default value if present
            if (param.DefaultValue != null)
            {
                paramSchema["default"] = param.DefaultValue;
            }
            
            // Add to schema properties
            var jsonSchema = JsonSerializer.Serialize(paramSchema);
            properties[param.Name] = JsonDocument.Parse(jsonSchema).RootElement;
            
            // Add to required list if mandatory
            if (param.Required)
            {
                required.Add(param.Name);
            }
        }
        
        // If there's a request body schema, add it as a nested property
        if (operation.RequestBodySchema != null)
        {
            var requestBodyJsonSchema = ConvertSchemaToJsonSchema(operation.RequestBodySchema);
            var serialized = JsonSerializer.Serialize(requestBodyJsonSchema);
            properties["requestBody"] = JsonDocument.Parse(serialized).RootElement;
            
            // If request body is required, add to required list
            if (operation.RequestBodySchema.Required?.Any() ?? false)
            {
                required.Add("requestBody");
            }
        }
        
        var schemaDict = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false
        };
        
        var schemaJson = JsonSerializer.Serialize(schemaDict);
        return new ToolInputSchema
        {
            Properties = properties,
            Required = required,
            AdditionalProperties = false
        };
    }
    
    private static Dictionary<string, object> ConvertSchemaToJsonSchema(SchemaInfo schemaInfo)
    {
        var jsonSchema = new Dictionary<string, object>();
        
        // Add type
        jsonSchema["type"] = schemaInfo.Type;
        
        // Add description
        if (!string.IsNullOrEmpty(schemaInfo.Description))
        {
            jsonSchema["description"] = schemaInfo.Description;
        }
        
        // Add enum if present
        if (schemaInfo.Enum != null && schemaInfo.Enum.Any())
        {
            jsonSchema["enum"] = schemaInfo.Enum;
        }
        
        // Add pattern if present
        if (!string.IsNullOrEmpty(schemaInfo.Pattern))
        {
            jsonSchema["pattern"] = schemaInfo.Pattern;
        }
        
        // Add min/max
        if (schemaInfo.Minimum.HasValue)
        {
            jsonSchema["minimum"] = schemaInfo.Minimum.Value;
        }
        
        if (schemaInfo.Maximum.HasValue)
        {
            jsonSchema["maximum"] = schemaInfo.Maximum.Value;
        }
        
        // Handle array type
        if (schemaInfo.Type == "array" && schemaInfo.Items != null)
        {
            jsonSchema["items"] = ConvertSchemaToJsonSchema(schemaInfo.Items);
        }
        
        // Handle object type
        if (schemaInfo.Type == "object" && schemaInfo.Properties.Any())
        {
            var properties = new Dictionary<string, object>();
            var requiredProperties = schemaInfo.Required ?? new List<string>();
            
            foreach (var prop in schemaInfo.Properties)
            {
                properties[prop.Key] = ConvertSchemaToJsonSchema(prop.Value);
            }
            
            jsonSchema["properties"] = properties;
            
            if (requiredProperties.Any())
            {
                jsonSchema["required"] = requiredProperties;
            }
        }
        
        return jsonSchema;
    }
    
    private static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        
        // Split by underscore or dash
        var parts = input.Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
        
        // Capitalize each part
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length > 0)
            {
                parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
            }
        }
        
        return string.Join("", parts);
    }
}