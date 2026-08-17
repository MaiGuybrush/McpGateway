using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace McpGateway.Core.Glossary;

/// <summary>
/// Tier 1 製造業/半導體/面板通用核心詞彙庫定義
/// </summary>
public static class CoreGlossary
{
    private static readonly List<GlossaryTerm> DefaultTier1Terms = new()
    {
        // 實體識別碼 (Entity Identifiers)
        new GlossaryTerm(
            Name: "productId",
            PascalName: "ProductId",
            Description: "產品料號或唯一代碼 (Product ID)",
            Category: "Entity",
            Aliases: new[] { "productCode", "prodId", "itemNo", "partNo", "materialNo", "PROD_ID" }
        ),
        new GlossaryTerm(
            Name: "productName",
            PascalName: "ProductName",
            Description: "產品品名或規格名稱",
            Category: "Entity",
            Aliases: new[] { "prodName", "itemDesc", "materialName", "PROD_NAME" }
        ),
        new GlossaryTerm(
            Name: "eqptId",
            PascalName: "EqptId",
            Description: "主設備/機台識別碼 (Equipment ID)",
            Category: "Entity",
            Aliases: new[] { "equipmentId", "machineId", "toolId", "machNo", "eqpId", "EQP_ID" }
        ),
        new GlossaryTerm(
            Name: "subEqptId",
            PascalName: "SubEqptId",
            Description: "子設備/單元/腔體機台 ID (Sub-Equipment ID)",
            Category: "Entity",
            Aliases: new[] { "subEquipmentId", "subMachineId", "unitId", "subToolId" }
        ),
        new GlossaryTerm(
            Name: "chamberId",
            PascalName: "ChamberId",
            Description: "機台腔體/處理槽代碼 (Chamber ID)",
            Category: "Entity",
            Aliases: new[] { "chId", "chamberNo", "slotId" }
        ),
        new GlossaryTerm(
            Name: "portId",
            PascalName: "PortId",
            Description: "裝卸埠/傳載埠代碼 (Port ID)",
            Category: "Entity",
            Aliases: new[] { "loadPortId", "portNo", "lpId" }
        ),
        new GlossaryTerm(
            Name: "workCenter",
            PascalName: "WorkCenter",
            Description: "生產工作中心/工作站代碼 (Work Center)",
            Category: "Entity",
            Aliases: new[] { "wc", "workCenterCode", "stationGroup" }
        ),
        new GlossaryTerm(
            Name: "productLine",
            PascalName: "ProductLine",
            Description: "生產線別代碼 (Product Line)",
            Category: "Entity",
            Aliases: new[] { "line", "productionLine", "lineNo", "lineId" }
        ),
        new GlossaryTerm(
            Name: "stepId",
            PascalName: "StepId",
            Description: "製程站點/工序代碼 (Step/Operation ID)",
            Category: "Entity",
            Aliases: new[] { "stageId", "operationId", "processId", "operId", "STEP_ID" }
        ),
        new GlossaryTerm(
            Name: "stepName",
            PascalName: "StepName",
            Description: "製程站點/工序名稱",
            Category: "Entity",
            Aliases: new[] { "operationName", "processName", "stageName" }
        ),
        new GlossaryTerm(
            Name: "recipeId",
            PascalName: "RecipeId",
            Description: "設備配方/製程程式代碼 (Recipe ID)",
            Category: "Entity",
            Aliases: new[] { "recipeName", "ppId", "recipeCode" }
        ),
        new GlossaryTerm(
            Name: "lotId",
            PascalName: "LotId",
            Description: "生產批號 (Lot ID)",
            Category: "Entity",
            Aliases: new[] { "lotNo", "batchId", "batchNo", "LOT_ID" }
        ),
        new GlossaryTerm(
            Name: "workOrderId",
            PascalName: "WorkOrderId",
            Description: "工單編號 (Work Order ID)",
            Category: "Entity",
            Aliases: new[] { "wo", "woId", "moId", "orderNo", "shopOrder", "WO_ID" }
        ),
        new GlossaryTerm(
            Name: "carrierId",
            PascalName: "CarrierId",
            Description: "載具/晶舟/卡匣編號 (Carrier/FOUP ID)",
            Category: "Entity",
            Aliases: new[] { "foupId", "cassetteId", "cstId", "carrierNo" }
        ),
        new GlossaryTerm(
            Name: "glassId",
            PascalName: "GlassId",
            Description: "面板玻璃基板 ID (Glass ID)",
            Category: "Entity",
            Aliases: new[] { "sheetId", "substrateId", "panelGlassId", "GLASS_ID" }
        ),
        new GlossaryTerm(
            Name: "panelId",
            PascalName: "PanelId",
            Description: "切割後面板/合板 ID (Panel ID)",
            Category: "Entity",
            Aliases: new[] { "pnlId", "subPanelId", "unitPanelId", "PANEL_ID" }
        ),
        new GlossaryTerm(
            Name: "waferId",
            PascalName: "WaferId",
            Description: "晶圓片 ID (Wafer ID)",
            Category: "Entity",
            Aliases: new[] { "waferNo", "dieId", "WAFER_ID" }
        ),

        // 生產計量與良率指標 (Metrics & Quantities)
        new GlossaryTerm(
            Name: "quantityInProcess",
            PascalName: "QuantityInProcess",
            Description: "目前在製中數量（尚未完工）",
            Category: "Metric",
            Aliases: new[] { "wipQty", "inProcessQty", "qtyInProcess" }
        ),
        new GlossaryTerm(
            Name: "quantityCompleted",
            PascalName: "QuantityCompleted",
            Description: "該工單/項目已完工入庫數量",
            Category: "Metric",
            Aliases: new[] { "completedQty", "doneQty", "finishQty" }
        ),
        new GlossaryTerm(
            Name: "totalItems",
            PascalName: "TotalItems",
            Description: "符合查詢條件的項目總筆數",
            Category: "Metric",
            Aliases: new[] { "totalCount", "itemCount", "total" }
        ),
        new GlossaryTerm(
            Name: "yieldRate",
            PascalName: "YieldRate",
            Description: "生產/檢驗良率（範圍 0.0 ~ 1.0）",
            Category: "Metric",
            Aliases: new[] { "yield", "yieldRatio", "passRate" }
        ),
        new GlossaryTerm(
            Name: "defectCount",
            PascalName: "DefectCount",
            Description: "缺陷/不良品數量",
            Category: "Metric",
            Aliases: new[] { "ngQty", "scrapQty", "failCount" }
        ),

        // 生產狀態與時間戳記 (Status & Timestamps)
        new GlossaryTerm(
            Name: "status",
            PascalName: "Status",
            Description: "目前狀態代碼或說明（如: In Progress, Completed）",
            Category: "Status",
            Aliases: new[] { "state", "statusCode" }
        ),
        new GlossaryTerm(
            Name: "priority",
            PascalName: "Priority",
            Description: "生產優先等級（如: Normal, High, Urgent）",
            Category: "Status",
            Aliases: new[] { "priorityCode", "priLevel" }
        ),
        new GlossaryTerm(
            Name: "scheduledCompletion",
            PascalName: "ScheduledCompletion",
            Description: "預計完工日期時間",
            Category: "Timestamp",
            Aliases: new[] { "estimatedCompletion", "targetDate", "dueTime" }
        ),
        new GlossaryTerm(
            Name: "generatedAt",
            PascalName: "GeneratedAt",
            Description: "報告/資料產出之 UTC 時間戳記",
            Category: "Timestamp",
            Aliases: new[] { "createTime", "reportTime", "generatedTime" }
        )
    };

    /// <summary>
    /// 取得所有 Tier 1 核心詞彙定義
    /// </summary>
    public static IReadOnlyList<GlossaryTerm> Tier1Terms => DefaultTier1Terms.AsReadOnly();

    /// <summary>
    /// 根據名稱或別名查找對應的標準詞彙（不分大小寫）
    /// </summary>
    public static GlossaryTerm? FindByAliasOrName(string nameOrAlias)
    {
        if (string.IsNullOrWhiteSpace(nameOrAlias)) return null;

        return DefaultTier1Terms.FirstOrDefault(t =>
            string.Equals(t.Name, nameOrAlias, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.PascalName, nameOrAlias, StringComparison.OrdinalIgnoreCase) ||
            t.Aliases.Any(a => string.Equals(a, nameOrAlias, StringComparison.OrdinalIgnoreCase)));
    }
}
