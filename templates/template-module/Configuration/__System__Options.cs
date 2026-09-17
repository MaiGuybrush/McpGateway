namespace McpGateway.__Department__.__System__.Configuration;

/// <summary>
/// __System__ 子系統專屬組態選項。
/// </summary>
public class __System__Options
{
    public const string SectionName = "McpGateway:Systems:__system__";

    public DownstreamOptions Downstream { get; set; } = new();
}

public class DownstreamOptions
{
    public string BaseUrl { get; set; } = "http://api.corp.local/__department__/__system__";
    public int TimeoutSeconds { get; set; } = 30;
}
