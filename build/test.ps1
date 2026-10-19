param(
    [string]$Configuration = "Release",
    [switch]$NoBuild
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

if (-not $NoBuild) {
    dotnet build "$root/LiteMon.sln" -c $Configuration
    if ($LASTEXITCODE -ne 0) { exit 1 }
}

$failed = @()
foreach ($dir in Get-ChildItem -Path "$root/tests" -Directory) {
    $csproj = Join-Path $dir.FullName ("{0}.csproj" -f $dir.Name)
    Write-Host "=== $($dir.Name)" -ForegroundColor Cyan
    & dotnet run --project $csproj -c $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { $failed += $dir.Name }
}

if ($failed.Count -gt 0) {
    Write-Host "失败项目: $($failed -join ', ')" -ForegroundColor Red
    exit 1
}
Write-Host "全部测试通过" -ForegroundColor Green
