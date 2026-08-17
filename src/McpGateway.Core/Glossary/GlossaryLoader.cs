using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace McpGateway.Core.Glossary;

/// <summary>
/// 負責載入並合併 Tier 1 (Core) 與 Tier 2 (Local mcp-glossary.json) 詞彙庫
/// </summary>
public static class GlossaryLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// 從 JSON 文本字串載入 Tier 2 詞彙庫
    /// </summary>
    public static IReadOnlyList<GlossaryTerm> LoadFromJson(string jsonText)
    {
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            return Array.Empty<GlossaryTerm>();
        }

        try
        {
            var config = JsonSerializer.Deserialize<LocalGlossaryConfig>(jsonText, JsonOptions);
            return config?.Terms ?? (IReadOnlyList<GlossaryTerm>)Array.Empty<GlossaryTerm>();
        }
        catch
        {
            return Array.Empty<GlossaryTerm>();
        }
    }

    /// <summary>
    /// 從檔案路徑載入 Tier 2 詞彙庫
    /// </summary>
    public static IReadOnlyList<GlossaryTerm> LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return Array.Empty<GlossaryTerm>();
        }

        var jsonText = File.ReadAllText(filePath);
        return LoadFromJson(jsonText);
    }

    /// <summary>
    /// 合併 Tier 1 核心詞彙與 Tier 2 本地自定義詞彙（本地詞庫若有同名詞彙則覆蓋）
    /// </summary>
    public static IReadOnlyList<GlossaryTerm> MergeGlossaries(
        IEnumerable<GlossaryTerm> tier1Terms,
        IEnumerable<GlossaryTerm> tier2Terms)
    {
        var dictionary = new Dictionary<string, GlossaryTerm>(StringComparer.OrdinalIgnoreCase);

        foreach (var term in tier1Terms)
        {
            dictionary[term.Name] = term;
        }

        foreach (var term in tier2Terms)
        {
            dictionary[term.Name] = term;
        }

        return dictionary.Values.ToList().AsReadOnly();
    }
}
