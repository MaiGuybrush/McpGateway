# 使用 `mcp-tool-dto-builder` Skill 的範例

## 指令

```
/skill:mcp-tool-dto-builder
```

## 規格輸入

### Tool Name

`QueryWipTool`

### MCP Tool Input

| 參數   | 可選值                                  |
| ------ | --------------------------------------- |
| `SHOP` | `TFT3` / `TFT6` / `TFT7` / `TFT8` / `TFT8B` / `TFTT6` |

### MCP Tool Output

```json
[
  {
    "procId": "",               // 目前所在站點 ID
    "productId": "TJ6F12BK",   // 產品 ID
    "ecCode": "CF",             // 產品版本
    "ownerId": "PROD",          // Owner：PROD 正常貨、ENG 工程貨、INT 整合
    "lotCnt": 2,                // LOT 數量（一個 LOT 代表多個 work 集合）
    "sheetCnt": 40,             // 玻璃 (work) 數量
    "panelCnt": 306,            // Panel 數量（單一玻璃存在多個 panel）
    "stayHours": 26.205,        // 當站停留時間
    "startHours": 304.969       // 下線時間
  }
]
```

## API 規格

### 呼叫方式

參考 `D:\Projects\agent\cim-agent\scripts\Query-WipData.ps1`，由環境變數提供 NTLM 帳密。

### API Input

無

### API Output Sample

```json
[
  {
    "PROC_ID": "",
    "PRODUCT_ID": "TJ6F12BK",
    "PRODUCT_VER": "CF",
    "OWNER_ID": "PROD",
    "LOT_CNT": 2,
    "SHEET_CNT": 40,
    "PANEL_CNT": 306,
    "STAY_HRS": 26.205,
    "START_HRS": 304.96944444444443
  },
  {
    "PROC_ID": "",
    "PRODUCT_ID": "TJ6F12DK",
    "PRODUCT_VER": "CF",
    "OWNER_ID": "PROD",
    "LOT_CNT": 2,
    "SHEET_CNT": 40,
    "PANEL_CNT": 316,
    "STAY_HRS": 15.018333333333333,
    "START_HRS": 343.99
  }
]
```