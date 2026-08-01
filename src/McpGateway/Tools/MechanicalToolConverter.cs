using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.OpenApi.Models;
using McpGateway.Infrastructure;

namespace McpGateway.Tools;

public interface IMechanicalToolConverter
{
    Task<List<ITool>> ConvertAsync(string openApiSpecPath);
    ITool ConvertOperation(OpenApiOperationInfo operationInfo);
}

public class MechanicalToolConverter : IMechanicalToolConverter
{
    private readonly IOpenApiParser _parser;
    private readonly IHttpClientFactory _httpClientFactory;

    public MechanicalToolConverter(
        IOpenApiParser parser,
        IHttpClientFactory httpClientFactory)
    {
        _parser = parser;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<List<ITool>> ConvertAsync(string openApiSpecPath)
    {
        var document = await _parser.ParseAsync(openApiSpecPath);
        var operations = _parser.GetOperations(document);
        
        var tools = new List<ITool>();
        
        foreach (var operation in operations)
        {
            var tool = ConvertOperation(operation);
            tools.Add(tool);
        }
        
        return tools;
    }

    public ITool ConvertOperation(OpenApiOperationInfo operationInfo)
    {
        var toolName = GenerateToolName(operationInfo);
        var description = operationInfo.Operation.Summary ?? operationInfo.Operation.Description ?? toolName;
        
        // 產生 JSON Schema for parameters
        var parameterSchema = GenerateParameterSchema(operationInfo);
        
        return new MechanicalTool(
            toolName,
            description,
            operationInfo,
            _httpClientFactory,
            parameterSchema);
    }

    private string GenerateToolName(OpenApiOperationInfo operationInfo)
    {
        // 從 path 和 method 產生 Tool 名稱
        // e.g., GET /api/users/{id} -> get_api_users_id
        var cleanPath = string.Join("_", 
            operationInfo.Path.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.TrimStart('{').TrimEnd('}')));
        
        var method = operationInfo.Method.ToString().ToLowerInvariant();
        return $"{method}_{cleanPath}";
    }

    private string GenerateParameterSchema(OpenApiOperationInfo operationInfo)
    {
        var schema = new {
            type = "object",
            properties = new Dictionary<string, object>(),
            required = new List<string>()
        };

        // Path parameters
        foreach (var param in operationInfo.Operation.Parameters.Where(p => p.In == ParameterLocation.Path))
        {
            schema.properties[param.Name] = ConvertOpenApiSchemaToJsonSchema(param.Schema);
            schema.required.Add(param.Name);
        }

        // Query parameters
        foreach (var param in operationInfo.Operation.Parameters.Where(p => p.In == ParameterLocation.Query))
        {
            schema.properties[param.Name] = ConvertOpenApiSchemaToJsonSchema(param.Schema);
            if (param.Required)
            {
                schema.required.Add(param.Name);
            }
        }

        // Request body
        if (operationInfo.Operation.RequestBody != null && 
            operationInfo.Operation.RequestBody.Content.TryGetValue("application/json", out var content))
        {
            var bodySchema = ConvertOpenApiSchemaToJsonSchema(content.Schema);
            schema.properties["requestBody"] = bodySchema;
            schema.required.Add("requestBody");
        }

        return JsonSerializer.Serialize(schema, new JsonSerializerOptions 
        { 
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull 
        });
    }

    private Dictionary<string, object> ConvertOpenApiSchemaToJsonSchema(OpenApiSchema schema)
    {
        var jsonSchema = new Dictionary<string, object>();

        switch (schema.Type)
        {
            case "integer":
                jsonSchema["type"] = "integer";
                if (schema.Format == "int32" || schema.Format == "int64")
                    jsonSchema["format"] = schema.Format;
                break;
            case "number":
                jsonSchema["type"] = "number";
                if (schema.Format == "float" || schema.Format == "double" || schema.Format == "decimal")
                    jsonSchema["format"] = schema.Format;
                break;
            case "string":
                jsonSchema["type"] = "string";
                if (!string.IsNullOrEmpty(schema.Format))
                    jsonSchema["format"] = schema.Format;
                break;
            case "boolean":
                jsonSchema["type"] = "boolean";
                break;
            case "array":
                jsonSchema["type"] = "array";
                if (schema.Items != null)
                    jsonSchema["items"] = ConvertOpenApiSchemaToJsonSchema(schema.Items);
                break;
            case "object":
                jsonSchema["type"] = "object";
                var properties = new Dictionary<string, object>();
                foreach (var prop in schema.Properties)
                {
                    properties[prop.Key] = ConvertOpenApiSchemaToJsonSchema(prop.Value);
                }
                jsonSchema["properties"] = properties;
                break;
        }

        if (!string.IsNullOrEmpty(schema.Description))
        {
            jsonSchema["description"] = schema.Description;
        }

        if (schema.Enum?.Any() == true)
        {
            jsonSchema["enum"] = schema.Enum.Select(e => e.ToString()).ToList();
        }

        return jsonSchema;
    }
}

public class MechanicalTool : ITool
{
    public string Name { get; }
    public string Description { get; }
    public string ParameterSchema { get; }
    
    private readonly OpenApiOperationInfo _operationInfo;
    private readonly IHttpClientFactory _httpClientFactory;

    public MechanicalTool(
        string name,
        string description,
        OpenApiOperationInfo operationInfo,
        IHttpClientFactory httpClientFactory,
        string parameterSchema)
    {
        Name = name;
        Description = description;
        ParameterSchema = parameterSchema;
        _operationInfo = operationInfo;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<dynamic> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var client = _httpClientFactory.CreateClient();
        
        var url = BuildUrl(parameters);
        
        using var request = new HttpRequestMessage(GetHttpMethod(_operationInfo.Method), url);
        
        if (parameters.ContainsKey("requestBody"))
        {
            var json = JsonSerializer.Serialize(parameters["requestBody"]);
            request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        }

        var response = await client.SendAsync(request);
        
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"API call failed: {response.StatusCode}");
        }

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<dynamic>(content) ?? new { };
    }

    private string BuildUrl(Dictionary<string, object> parameters)
    {
        var url = _operationInfo.Path;
        
        // Replace path parameters
        foreach (var param in _operationInfo.Operation.Parameters.Where(p => p.In == ParameterLocation.Path))
        {
            if (parameters.TryGetValue(param.Name, out var value))
            {
                url = url.Replace($"{{{param.Name}}}", value.ToString());
            }
        }
        
        // Add query parameters
        var queryParams = new List<string>();
        foreach (var param in _operationInfo.Operation.Parameters.Where(p => p.In == ParameterLocation.Query))
        {
            if (parameters.TryGetValue(param.Name, out var value))
            {
                queryParams.Add($"{param.Name}={Uri.EscapeDataString(value.ToString() ?? "")}");
            }
        }
        
        if (queryParams.Any())
        {
            url += "?" + string.Join("&", queryParams);
        }
        
        return "http://localhost:5001" + url;
    }

    private static HttpMethod GetHttpMethod(OperationType method) => method switch
    {
        OperationType.Get => HttpMethod.Get,
        OperationType.Post => HttpMethod.Post,
        OperationType.Put => HttpMethod.Put,
        OperationType.Delete => HttpMethod.Delete,
        OperationType.Patch => HttpMethod.Patch,
        OperationType.Head => HttpMethod.Head,
        OperationType.Options => HttpMethod.Options,
        _ => HttpMethod.Get
    };
}