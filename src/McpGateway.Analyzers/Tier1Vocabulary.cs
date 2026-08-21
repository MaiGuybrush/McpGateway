using System;
using System.Collections.Generic;
using System.Linq;

namespace McpGateway.Analyzers;

internal class GlossaryEntry
{
    public string StandardCamel { get; }
    public string StandardPascal { get; }
    public string Description { get; }
    public string[] Aliases { get; }

    public GlossaryEntry(string standardCamel, string standardPascal, string description, string[] aliases)
    {
        StandardCamel = standardCamel;
        StandardPascal = standardPascal;
        Description = description;
        Aliases = aliases;
    }
}

internal static class Tier1Vocabulary
{
    public static readonly GlossaryEntry[] Entries = new[]
    {
        // 實體識別碼
        new GlossaryEntry("productId", "ProductId", "產品料號或唯一代碼 (Product ID)", new[] { "productCode", "prodId", "itemNo", "partNo", "materialNo", "PROD_ID" }),
        new GlossaryEntry("productName", "ProductName", "產品品名或規格名稱", new[] { "prodName", "itemDesc", "PROD_DESC", "PROD_NAME" }),
        new GlossaryEntry("eqptId", "EqptId", "主設備/機台識別碼 (Equipment ID)", new[] { "equipmentId", "machineId", "toolId", "machNo", "eqpId", "EQP_ID" }),
        new GlossaryEntry("subEqptId", "SubEqptId", "子設備/Unit/Chamber機台 ID", new[] { "subEquipmentId", "subMachineId", "unitId", "subToolId" }),
        new GlossaryEntry("chamberId", "ChamberId", "子機台/Chamber", new[] { "chId", "chamberNo", "slotId" }),
        new GlossaryEntry("portId", "PortId", "裝卸埠/傳載埠代碼", new[] { "port_id", "portNo", "lpId" }),
        new GlossaryEntry("recipeId", "RecipeId", "設備配方/製程程式代碼", new[] { "recipeName", "ppId", "recipeCode" }),
        new GlossaryEntry("lotId", "LotId", "生產批號", new[] { "lotNo", "batchId", "batchNo", "LOT_ID" }),
        new GlossaryEntry("workOrderId", "WorkOrderId", "工單編號", new[] { "wo", "woId", "moId", "orderNo", "shopOrder", "WO_ID" }),
        new GlossaryEntry("carrierId", "CarrierId", "載具/晶舟/卡匣編號/CST ID/cassette ID", new[] { "foupId", "cassetteId", "cstId", "carrierNo" }),
        new GlossaryEntry("glassId", "GlassId", "面板玻璃基板 ID", new[] { "sheetId", "substrateId", "panelGlassId", "GLASS_ID" }),
        new GlossaryEntry("panelId", "PanelId", "切割後面板/合板 ID", new[] { "pnlId", "subPanelId", "unitPanelId", "PANEL_ID" }),
        new GlossaryEntry("processId", "ProcessId", "製程代碼/工藝代碼", new[] { "procId", "processCode", "processName" }),
        new GlossaryEntry("ownerId", "OwnerId", "批貨擁有者類別 (Owner ID)", new[] { "ownerType", "ownerCode" }),
        new GlossaryEntry("ecCode", "EcCode", "產品版本/工程變更代碼 (Engineering Change Code)", new[] { "engineeringChangeCode", "ecNo" }),
        new GlossaryEntry("lotCnt", "LotCnt", "LOT 數量 (Lot Count)", new[] { "lotCount", "batchCount" }),
        new GlossaryEntry("sheetCnt", "SheetCnt", "玻璃數量 (Sheet Count)", new[] { "sheetCount", "substrateCount" }),
        new GlossaryEntry("panelCnt", "PanelCnt", "Panel 數量 (Panel Count)", new[] { "panelCount", "unitPanelCount" }),
        new GlossaryEntry("stayHours", "StayHours", "當站停留時間 (Stay Duration in Hours)", new[] { "stayTime", "dwellTime", "stationStayHours" }),
        new GlossaryEntry("startHours", "StartHours", "下線時間 (Start Duration in Hours)", new[] { "startTime", "downTime", "startDurationHours" }),
        
        
        // 生產計量與指標

        // 狀態與時間戳記
    };

    public static GlossaryEntry? FindByAlias(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        return Entries.FirstOrDefault(e =>
            e.Aliases.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase)));
    }
}
