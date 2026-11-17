param(
    [string]$RitsuLibRoot = "C:\Game\Steam\steamapps\workshop\content\2868840\3747602295",
    [string]$Manifest = (Join-Path (Split-Path $PSScriptRoot -Parent) "mod_manifest.json"),
    [switch]$FailOnNewer
)

$ErrorActionPreference = "Stop"

function Convert-RitsuVersion([string]$Text) {
    $match = [regex]::Match($Text, '^(\d+)\.(\d+)\.(\d+)')
    if (-not $match.Success) {
        throw "无法解析版本号：$Text"
    }
    return [version]::new(
        [int]$match.Groups[1].Value,
        [int]$match.Groups[2].Value,
        [int]$match.Groups[3].Value)
}

$installedManifest = Join-Path $RitsuLibRoot "mod_manifest.json"
if (-not (Test-Path -LiteralPath $installedManifest)) {
    throw "找不到已安装的 RitsuLib：$installedManifest"
}
if (-not (Test-Path -LiteralPath $Manifest)) {
    throw "找不到模组清单：$Manifest"
}

$installedText = (Get-Content -LiteralPath $installedManifest -Raw | ConvertFrom-Json).version
$project = Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json
$dependency = @($project.dependencies | Where-Object { $_.id -eq "STS2-RitsuLib" }) | Select-Object -First 1
if ($null -eq $dependency -or [string]::IsNullOrWhiteSpace($dependency.min_version)) {
    throw "清单中没有 STS2-RitsuLib 的 min_version。"
}

$installedVersion = Convert-RitsuVersion $installedText
$declaredVersion = Convert-RitsuVersion $dependency.min_version

Write-Host "RitsuLib installed : $installedText"
Write-Host "Manifest minimum   : $($dependency.min_version)"

if ($installedVersion -lt $declaredVersion) {
    Write-Error "RitsuLib 低于清单下限。先更新 RitsuLib，再重新构建。"
    exit 2
}

if ($installedVersion -gt $declaredVersion) {
    Write-Warning "RitsuLib 已高于清单下限。把 min_version 更新到最新版，并重新测试。"
    if ($FailOnNewer) {
        exit 1
    }
    exit 0
}

Write-Host "RitsuLib 与清单下限一致。"
exit 0
