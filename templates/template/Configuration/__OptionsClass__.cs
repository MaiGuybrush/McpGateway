using System.Collections.Generic;

namespace McpGateway.__Department__.Configuration;

public class __OptionsClass__
{
    public const string SectionName = "__Department__";

    public string BaseUrl { get; set; } = "http://api.corp.local/__department__";
    public int TimeoutSeconds { get; set; } = 30;
    public int CacheMinutes { get; set; } = 3;
    public List<string> ConsulUrls { get; set; } = new();
    public string ConsulKey { get; set; } = "__Department__ShopList";
    public Dictionary<string, string> FallbackShops { get; set; } = new()
    {
        { "TFT1", "http://tft1-api.corp.local/__department__" }
    };
}
