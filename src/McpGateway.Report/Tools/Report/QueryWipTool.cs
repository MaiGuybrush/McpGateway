using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace McpGateway.Report.Tools.Report;

/// <summary>
/// 查詢在製品（WIP）報告，提供製造現場的生產進度、產量與良率資訊
/// </summary>
[McpServerToolType]
public class QueryWipTool
{
    [McpServerTool]
    [Description("查詢在製品（WIP）報告，提供製造現場的生產進度、產量與良率資訊")]
    public static string QueryWip(
        [Description("工作中心編號（選填）")]
        string? workCenter = null,
        
        [Description("產品線編號（選填）")]
        string? productLine = null,
        
        [Description("開始日期（選填），格式: YYYY-MM-DD")]
        string? startDate = null,
        
        [Description("結束日期（選填），格式: YYYY-MM-DD")]
        string? endDate = null)
    {
        var wipReport = new WipReport
        {
            TotalItems = 5,
            Items = new List<WipItem>
            {
                new WipItem
                {
                    WorkOrderId = "WO-1234",
                    ProductCode = "PROD-001",
                    ProductName = "Widget A",
                    WorkCenter = workCenter ?? "WC-1",
                    ProductLine = productLine ?? "LINE-1",
                    QuantityInProcess = 150,
                    QuantityCompleted = 75,
                    Status = "In Progress",
                    ScheduledCompletion = DateTime.UtcNow.AddDays(7),
                    Priority = "Normal",
                    YieldRate = 0.95
                }
            },
            Summary = new WipSummary
            {
                TotalQuantityInProcess = 150,
                TotalQuantityCompleted = 75,
                AverageYieldRate = 0.95,
                WorkCenters = 1,
                ProductLines = 1
            },
            GeneratedAt = DateTime.UtcNow
        };

        return FormatWipReport(wipReport);
    }

    private static string FormatWipReport(WipReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## WIP 在製品報告");
        sb.AppendLine($"報告產生時間: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"總項目數: {report.TotalItems}");
        sb.AppendLine();
        
        sb.AppendLine("### 摘要統計");
        sb.AppendLine($"- 在製總數: {report.Summary.TotalQuantityInProcess:N0}");
        sb.AppendLine($"- 完成總數: {report.Summary.TotalQuantityCompleted:N0}");
        sb.AppendLine($"- 平均良率: {report.Summary.AverageYieldRate:P2}");
        sb.AppendLine($"- 工作中心數: {report.Summary.WorkCenters}");
        sb.AppendLine($"- 產品線數: {report.Summary.ProductLines}");
        sb.AppendLine();
        
        sb.AppendLine("### 明細項目");
        foreach (var item in report.Items.Take(10))
        {
            sb.AppendLine($"- **工單號**: {item.WorkOrderId}");
            sb.AppendLine($"  - 產品: {item.ProductCode} - {item.ProductName}");
            sb.AppendLine($"  - 工作中心: {item.WorkCenter}, 產品線: {item.ProductLine}");
            sb.AppendLine($"  - 在製數量: {item.QuantityInProcess:N0}, 完成數量: {item.QuantityCompleted:N0}");
            sb.AppendLine($"  - 狀態: {item.Status}, 優先級: {item.Priority}");
            sb.AppendLine($"  - 預計完成: {item.ScheduledCompletion:yyyy-MM-dd}");
            sb.AppendLine($"  - 良率: {item.YieldRate:P2}");
            sb.AppendLine();
        }
        
        if (report.Items.Count > 10)
            sb.AppendLine($"... 還有 {report.Items.Count - 10} 個項目未顯示");
        
        return sb.ToString();
    }
}

// DTO classes
public class WipReport
{
    public int TotalItems { get; set; }
    public List<WipItem> Items { get; set; } = new();
    public WipSummary Summary { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public class WipItem
{
    public string WorkOrderId { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string WorkCenter { get; set; } = string.Empty;
    public string ProductLine { get; set; } = string.Empty;
    public int QuantityInProcess { get; set; }
    public int QuantityCompleted { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ScheduledCompletion { get; set; }
    public string Priority { get; set; } = string.Empty;
    public double YieldRate { get; set; }
}

public class WipSummary
{
    public int TotalQuantityInProcess { get; set; }
    public int TotalQuantityCompleted { get; set; }
    public double AverageYieldRate { get; set; }
    public int WorkCenters { get; set; }
    public int ProductLines { get; set; }
}