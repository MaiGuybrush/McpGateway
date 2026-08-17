using System;
using System.Collections.Generic;

namespace McpGateway.Core.Glossary;

/// <summary>
/// 表示領域詞彙庫中的單一詞彙定義
/// </summary>
public record GlossaryTerm(
    string Name,
    string PascalName,
    string Description,
    string Category,
    IReadOnlyList<string> Aliases
)
{
    /// <summary>
    /// 無參數建構子供反序列化使用
    /// </summary>
    public GlossaryTerm() : this(string.Empty, string.Empty, string.Empty, string.Empty, Array.Empty<string>()) { }
}

/// <summary>
/// 表示本地 mcp-glossary.json 的根結構
/// </summary>
public class LocalGlossaryConfig
{
    public string Schema { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public List<GlossaryTerm> Terms { get; set; } = new();
}
