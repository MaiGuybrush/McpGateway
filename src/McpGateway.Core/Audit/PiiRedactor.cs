using System.Text.Json;
using System.Text.Json.Nodes;

namespace McpGateway.Core.Audit;

/// <summary>
/// Redacts PII from data structures according to Core spec §8.
/// Email: c***@example.com format
/// Other fields: first 2 characters + ***
/// Masking happens before writing to any sink.
/// </summary>
public class PiiRedactor
{
    private readonly HashSet<string> _piiFields;

    public PiiRedactor(IEnumerable<string> piiFields)
    {
        _piiFields = new HashSet<string>(piiFields, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Redacts PII from an object recursively.
    /// </summary>
    public object Redact(object data)
    {
        if (data == null) return data!;

        return RedactObject(data);
    }

    private object RedactObject(object obj)
    {
        // Handle primitive types
        if (obj is string str)
        {
            return str; // Return as-is, field name is needed for masking
        }

        if (obj is JsonElement jsonElement)
        {
            return RedactJsonElement(jsonElement);
        }

        // Handle collections
        if (obj is System.Collections.IEnumerable enumerable && obj is not string)
        {
            var list = new List<object?>();
            foreach (var item in enumerable)
            {
                list.Add(RedactObject(item!));
            }
            return list;
        }

        // Handle dictionary
        if (obj is IDictionary<string, object?> dict)
        {
            var redactedDict = new Dictionary<string, object?>();
            foreach (var kvp in dict)
            {
                redactedDict[kvp.Key] = IsPiiField(kvp.Key) 
                    ? MaskValue(kvp.Value, kvp.Key)
                    : RedactObject(kvp.Value!);
            }
            return redactedDict;
        }

        // Handle JSON object
        if (obj is JsonNode jsonNode)
        {
            return RedactJsonNode(jsonNode);
        }

        // For other complex objects, convert to dictionary and redact
        try
        {
            var json = JsonSerializer.Serialize(obj);
            var document = JsonDocument.Parse(json);
            return RedactJsonElement(document.RootElement);
        }
        catch
        {
            // Fall back to string representation if serialization fails
            return obj.ToString() ?? string.Empty;
        }
    }

    private JsonNode RedactJsonNode(JsonNode node)
    {
        if (node is JsonObject jsonObject)
        {
            var redacted = new JsonObject();
            foreach (var property in jsonObject)
            {
                redacted[property.Key] = IsPiiField(property.Key)
                    ? JsonValue.Create(MaskValue(property.Value, property.Key))
                    : RedactJsonNode(property.Value!);
            }
            return redacted;
        }
        else if (node is JsonArray jsonArray)
        {
            var array = new JsonArray();
            foreach (var item in jsonArray)
            {
                array.Add(RedactJsonNode(item!));
            }
            return array;
        }
        else
        {
            return node;
        }
    }

    private object RedactJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var objDict = new Dictionary<string, object?>();
                foreach (var property in element.EnumerateObject())
                {
                    objDict[property.Name] = IsPiiField(property.Name)
                        ? MaskValue(property.Value, property.Name)
                        : RedactJsonElement(property.Value);
                }
                return objDict;

            case JsonValueKind.Array:
                var array = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    array.Add(RedactJsonElement(item));
                }
                return array;

            case JsonValueKind.String:
                return element.GetString() ?? string.Empty;

            case JsonValueKind.Number:
                return element.TryGetInt64(out var longValue) ? longValue : element.GetDouble();

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            case JsonValueKind.Null:
                return null!;

            default:
                return element.ToString() ?? string.Empty;
        }
    }

    private bool IsPiiField(string fieldName)
    {
        return _piiFields.Contains(fieldName);
    }

    private object? MaskValue(object? value, string fieldName)
    {
        if (value == null) return null;

        var str = value.ToString();
        if (string.IsNullOrEmpty(str)) return string.Empty;

        // Email format: c***@example.com
        if (fieldName.Equals("email", StringComparison.OrdinalIgnoreCase) || 
            str.Contains('@'))
        {
            var atIndex = str.IndexOf('@');
            if (atIndex > 0)
            {
                var localPart = str.Substring(0, atIndex);
                var domain = str.Substring(atIndex);
                var firstChar = localPart.Length > 0 ? localPart[0].ToString() : "";
                return $"{firstChar}***{domain}";
            }
        }

        // Default: first 2 characters + ***
        var prefixLength = Math.Min(2, str.Length);
        var prefix = str.Substring(0, prefixLength);
        return $"{prefix}***";
    }

    private object? MaskValue(JsonElement element, string fieldName)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            return RedactJsonElement(element);
        }

        var str = element.GetString() ?? string.Empty;
        
        // Email format: c***@example.com
        if (fieldName.Equals("email", StringComparison.OrdinalIgnoreCase) ||
            str.Contains('@'))
        {
            var atIndex = str.IndexOf('@');
            if (atIndex > 0)
            {
                var localPart = str.Substring(0, atIndex);
                var domain = str.Substring(atIndex);
                var firstChar = localPart.Length > 0 ? localPart[0].ToString() : "";
                return $"{firstChar}***{domain}";
            }
        }

        // Default: first 2 characters + ***
        var prefixLength = Math.Min(2, str.Length);
        var prefix = str.Substring(0, prefixLength);
        return $"{prefix}***";
    }
}