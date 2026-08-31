<#
.SYNOPSIS
    MCP Gateway 部門專案腳手架 (Scaffold) 自動化產生腳本

.DESCRIPTION
    依據 MCP Gateway 架構標準，自動化建立符合規範之獨立部門 Gateway C# .NET 9 專案。

.PARAMETER Department
    部門名稱代號 (如 eap, spc, mfg_report)

.PARAMETER Port
    服務監聽通訊埠 (如 5200)

.PARAMETER ToolName
    主要 MCP 工具名稱 (必須以 <department>_ 開頭，如 eap_query_lot)

.PARAMETER AuthProvider
    認證提供者 (API-KEY, JWT, NTLM, None，預設為 API-KEY)

.PARAMETER UseConsul
    是否啟用 Consul 集中式服務發現設定 (預設為 $true)

.PARAMETER Solution
    是否建立 .sln 方案檔

.PARAMETER OutDir
    輸出目錄路徑 (預設為當前目錄)

.PARAMETER DryRun
    預覽模式 (不寫入磁碟)

.PARAMETER Force
    若目標目錄已存在則強制覆寫

.EXAMPLE
    pwsh .\scaffold.ps1 -Department eap -Port 5200 -ToolName eap_query_lot -AuthProvider API-KEY -OutDir D:\Projects\.NET
#>

[CmdletBinding()]
param (
    [Parameter(Mandatory = $true, HelpMessage = "部門名稱代號 (如 eap, spc, mfg_report)")]
    [ValidatePattern('^[a-zA-Z][a-zA-Z0-9_]*$')]
    [string]$Department,

    [Parameter(Mandatory = $true, HelpMessage = "服務監聽通訊埠 (如 5200)")]
    [ValidateRange(1024, 65535)]
    [int]$Port,

    [Parameter(Mandatory = $true, HelpMessage = "主要 MCP 工具名稱 (必須以 <department>_ 開頭，如 eap_query_lot)")]
    [ValidatePattern('^[a-z0-9_]+$')]
    [string]$ToolName,

    [Parameter(Mandatory = $false, HelpMessage = "認證提供者")]
    [ValidateSet('API-KEY', 'JWT', 'NTLM', 'None')]
    [string]$AuthProvider = 'API-KEY',

    [Parameter(Mandatory = $false, HelpMessage = "是否啟用 Consul 集中式服務發現設定")]
    [object]$UseConsul = $true,

    [Parameter(Mandatory = $false, HelpMessage = "是否建立 .sln 方案檔")]
    [switch]$Solution,

    [Parameter(Mandatory = $false, HelpMessage = "輸出目錄路徑 (預設為當前目錄)")]
    [string]$OutDir = '.\',

    [Parameter(Mandatory = $false, HelpMessage = "預覽模式 (不寫入磁碟)")]
    [switch]$DryRun,

    [Parameter(Mandatory = $false, HelpMessage = "若目標目錄已存在則強制覆寫")]
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-PascalCase ([string]$text) {
    if ([string]::IsNullOrWhiteSpace($text)) { return "" }
    $parts = $text -split '[_\-]'
    $pascal = ""
    foreach ($part in $parts) {
        if ($part.Length -gt 0) {
            $pascal += $part.Substring(0, 1).ToUpperInvariant() + $part.Substring(1).ToLowerInvariant()
        }
    }
    return $pascal
}

# 0. 解析 UseConsul 布林值
$isUseConsul = $true
if ($null -ne $UseConsul) {
    if ($UseConsul -is [bool]) {
        $isUseConsul = $UseConsul
    } elseif ($UseConsul -is [string]) {
        $isUseConsul = ($UseConsul.ToLowerInvariant() -in @('$true', 'true', '1', 'yes'))
    } elseif ($UseConsul -is [int]) {
        $isUseConsul = ($UseConsul -ne 0)
    }
}
$UseConsul = $isUseConsul

# 1. 驗證部門名稱長度
if ($Department.Length -lt 2 -or $Department.Length -gt 20) {
    Write-Error "部門名稱長度必須在 2 到 20 個字元之間。"
    exit 1
}

# 2. 驗證保留字
$reservedWords = @('core', 'common', 'shared', 'test', 'tests', 'gateway', 'base', 'mock', 'mcp', 'template')
if ($reservedWords -contains $Department.ToLowerInvariant()) {
    Write-Error "部門名稱 '$Department' 為系統保留字，請使用具體業務部門名稱。"
    exit 1
}

# 3. 驗證 ToolName 前綴
$deptLower = $Department.ToLowerInvariant()
$expectedPrefix = "${deptLower}_"
if (-not $ToolName.StartsWith($expectedPrefix)) {
    Write-Error "主要工具名稱 '$ToolName' 必須以部門前綴 '${expectedPrefix}' 開頭 (例如 '${expectedPrefix}query_xxx')。"
    exit 1
}

# 4. 計算名稱映射
$pascalDept = ConvertTo-PascalCase $Department
$toolRemainder = $ToolName.Substring($expectedPrefix.Length)
$toolClass = ConvertTo-PascalCase $toolRemainder
if ([string]::IsNullOrWhiteSpace($toolClass)) {
    $toolClass = "Default"
}
$optionsClass = "${toolClass}Options"
$projectName = "McpGateway.$pascalDept"

# 5. 動態解析 Core 版本
$coreCsprojPath = Join-Path $PSScriptRoot "..\src\McpGateway.Core\McpGateway.Core.csproj"
$coreVersion = "0.2.0-preview"
if (Test-Path $coreCsprojPath) {
    try {
        $xml = [xml](Get-Content $coreCsprojPath)
        $verNode = $xml.SelectSingleNode("//PropertyGroup/Version")
        if ($verNode -and -not [string]::IsNullOrWhiteSpace($verNode.InnerText)) {
            $coreVersion = $verNode.InnerText.Trim()
        }
    }
    catch {
        # Fallback to default version if XML parsing fails
    }
}

# 6. 載入 Auth Snippet
$authSnippetFile = switch ($AuthProvider.ToUpperInvariant()) {
    'API-KEY' { 'api-key.json' }
    'JWT'     { 'jwt.json' }
    'NTLM'    { 'ntlm.json' }
    'NONE'    { 'none.json' }
    default   { 'api-key.json' }
}
$authSnippetPath = Join-Path $PSScriptRoot "snippets\auth\$authSnippetFile"
$authSnippetContent = ""
if (Test-Path $authSnippetPath) {
    $authSnippetContent = Get-Content $authSnippetPath -Raw
    $authSnippetContent = $authSnippetContent.Replace('__department__', $deptLower)
}

# 7. 計算目標路徑與衝突防護
$targetDirPath = [System.IO.Path]::GetFullPath((Join-Path $OutDir $projectName))

if (Test-Path $targetDirPath) {
    $existingItems = Get-ChildItem $targetDirPath -Force -ErrorAction SilentlyContinue
    if ($existingItems -and $existingItems.Count -gt 0) {
        if (-not $Force -and -not $DryRun) {
            Write-Error "目標目錄 '$targetDirPath' 已存在且非空。請指定 -Force 參數進行覆寫，或更換輸出目錄。"
            exit 1
        }
    }
}

# 8. 替換規則表
$replacementList = @(
    @{ Key = '__Department__';   Value = $pascalDept },
    @{ Key = '__department__';   Value = $deptLower },
    @{ Key = '__Port__';         Value = $Port.ToString() },
    @{ Key = '__ToolClass__';    Value = $toolClass },
    @{ Key = '__tool_name__';    Value = $ToolName },
    @{ Key = '__OptionsClass__'; Value = $optionsClass },
    @{ Key = '__Namespace__';    Value = $projectName },
    @{ Key = '__CoreVersion__';  Value = $coreVersion },
    @{ Key = '__AuthSection__';  Value = $authSnippetContent.TrimEnd() }
)

$pathReplacements = @(
    @{ Key = '__Department__';   Value = $pascalDept },
    @{ Key = '__department__';   Value = $deptLower },
    @{ Key = '__ToolClass__';    Value = $toolClass },
    @{ Key = '__OptionsClass__'; Value = $optionsClass }
)

$templateRoot = Join-Path $PSScriptRoot "template"

# 9. DryRun 模式
if ($DryRun) {
    Write-Host "================================================================================" -ForegroundColor Cyan
    Write-Host "  [DryRun 預覽模式] MCP Gateway 部門專案產生清單" -ForegroundColor Cyan
    Write-Host "================================================================================" -ForegroundColor Cyan
    Write-Host "  專案名稱:    $projectName"
    Write-Host "  根命名空間:  $projectName"
    Write-Host "  通訊埠:      $Port (http://localhost:$Port)"
    Write-Host "  認證方式:    $AuthProvider"
    Write-Host "  主要工具:    $ToolName ($toolClass)"
    Write-Host "  Core 版本:   $coreVersion"
    Write-Host "  輸出路徑:    $targetDirPath"
    Write-Host "  Consul 整合: $(if ($UseConsul) { '啟用' } else { '停用' })"
    Write-Host "  建立 .sln:   $(if ($Solution) { '是' } else { '否' })"
    Write-Host "--------------------------------------------------------------------------------"
    Write-Host "預計產生之檔案與目錄清單：" -ForegroundColor Yellow

    $templateItems = Get-ChildItem -Recurse $templateRoot
    foreach ($item in $templateItems) {
        $relPath = $item.FullName.Substring($templateRoot.Length).TrimStart('\', '/')
        $destRelPath = $relPath
        foreach ($r in $pathReplacements) {
            $destRelPath = $destRelPath.Replace($r.Key, $r.Value)
        }
        if ($item.PSIsContainer) {
            Write-Host "  [DIR ] $destRelPath" -ForegroundColor DarkGray
        } else {
            Write-Host "  [FILE] $destRelPath" -ForegroundColor Green
        }
    }
    if ($Solution) {
        Write-Host "  [FILE] $projectName.sln" -ForegroundColor Green
    }
    Write-Host "================================================================================" -ForegroundColor Cyan
    return
}

# 10. 執行實際產生
if (-not (Test-Path $targetDirPath)) {
    New-Item -ItemType Directory -Path $targetDirPath -Force | Out-Null
}

$allTemplateItems = Get-ChildItem -Recurse $templateRoot

foreach ($item in $allTemplateItems) {
    $relPath = $item.FullName.Substring($templateRoot.Length).TrimStart('\', '/')
    $destRelPath = $relPath
    foreach ($r in $pathReplacements) {
        $destRelPath = $destRelPath.Replace($r.Key, $r.Value)
    }
    $destFullPath = Join-Path $targetDirPath $destRelPath

    if ($item.PSIsContainer) {
        if (-not (Test-Path $destFullPath)) {
            New-Item -ItemType Directory -Path $destFullPath -Force | Out-Null
        }
    } else {
        $parentDir = Split-Path $destFullPath -Parent
        if (-not (Test-Path $parentDir)) {
            New-Item -ItemType Directory -Path $parentDir -Force | Out-Null
        }

        $content = Get-Content $item.FullName -Raw
        foreach ($entry in $replacementList) {
            $content = $content.Replace($entry.Key, $entry.Value)
        }

        if (-not $UseConsul) {
            # 關閉 Consul URLs 設定
            $content = [System.Text.RegularExpressions.Regex]::Replace(
                $content,
                '"Urls":\s*\[[^\]]*\]',
                '"Urls": []'
            )
            $content = [System.Text.RegularExpressions.Regex]::Replace(
                $content,
                '"ConsulUrls":\s*\[[^\]]*\]',
                '"ConsulUrls": []'
            )
        }

        [System.IO.File]::WriteAllText($destFullPath, $content, [System.Text.Encoding]::UTF8)
    }
}

if ($Solution) {
    $slnPath = Join-Path $targetDirPath "$projectName.sln"
    $csprojPath = Join-Path $targetDirPath "$projectName.csproj"
    dotnet new sln -n $projectName -o $targetDirPath --force | Out-Null
    dotnet sln $slnPath add $csprojPath | Out-Null
}

# 11. 輸出成功摘要與指引
Write-Host "================================================================================" -ForegroundColor Green
Write-Host "  MCP Gateway 部門專案產生成功！" -ForegroundColor Green
Write-Host "================================================================================" -ForegroundColor Green
Write-Host "  專案名稱:    $projectName"
Write-Host "  根命名空間:  $projectName"
Write-Host "  通訊埠:      $Port (http://localhost:$Port)"
Write-Host "  認證方式:    $AuthProvider"
Write-Host "  主要工具:    $ToolName"
Write-Host "  Core 版本:   $coreVersion"
Write-Host "  輸出路徑:    $targetDirPath"
Write-Host "================================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "下一步操作指引："
Write-Host "  1. 切換至專案目錄："
Write-Host "     cd $targetDirPath"
Write-Host ""
Write-Host "  2. 還原與驗證 NuGet 套件："
Write-Host "     dotnet restore"
Write-Host ""
Write-Host "  3. 驗證編譯與 Roslyn 分析器："
Write-Host "     dotnet build"
Write-Host ""
Write-Host "  4. 調整下游 API 設定："
Write-Host "     開啟 appsettings.json 設定 $pascalDept 業務段之 BaseUrl 與 ConsulKey"
Write-Host ""
Write-Host "  5. 實作業務邏輯："
Write-Host "     - Tools/$toolClass/${toolClass}Tool.cs (定義輸入/輸出 DTO)"
Write-Host "     - Services/${toolClass}Service.cs (實作下游 HTTP 呼叫與快取)"
Write-Host ""
Write-Host "  6. 啟動服務測試："
Write-Host "     dotnet run"
Write-Host "     curl http://localhost:$Port/mcp"
