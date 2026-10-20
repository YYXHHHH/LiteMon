param(
    [string]$Runtime = "win-x64",
    [switch]$SkipLite
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$release = Join-Path $root "release"

function Publish-App([string]$name, [bool]$selfContained, [bool]$singleFile) {
    $out = Join-Path $release $name
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }

    Write-Host "publishing $name (self-contained=$selfContained single-file=$singleFile) ..." -ForegroundColor Cyan

    $arguments = @(
        "publish", (Join-Path $root "src/LiteMon.App/LiteMon.App.csproj"),
        "-c", "Release", "-r", $Runtime,
        $(if ($selfContained) { "--self-contained" } else { "--no-self-contained" }),
        "-p:PublishSingleFile=$($singleFile.ToString().ToLower())",
        "-p:EnableCompressionInSingleFile=false",
        "-p:PublishReadyToRun=$($selfContained.ToString().ToLower())",
        "-p:DebugType=none", "-p:DebugSymbols=false",
        "-o", $out
    )

    if ($singleFile) { $arguments += "-p:IncludeNativeLibrariesForSelfExtract=false" }

    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "publish failed: $name" }
}

Publish-App "LiteMon-portable-$Runtime" $true $false
if (-not $SkipLite) { Publish-App "LiteMon-lite-$Runtime" $false $true }

foreach ($dir in Get-ChildItem $release -Directory) {
    Copy-Item (Join-Path $root "build/README-portable.txt") (Join-Path $dir.FullName "README.txt") -Force
    $zip = "$($dir.FullName).zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $dir.FullName "*") -DestinationPath $zip -CompressionLevel Optimal
    $size = (Get-ChildItem $dir.FullName -Recurse | Measure-Object Length -Sum).Sum / 1MB
    $zipSize = (Get-Item $zip).Length / 1MB
    $files = (Get-ChildItem $dir.FullName -Recurse -File).Count
    "{0,-30} {1,7:N1} MB   zip {2,6:N1} MB   files {3}" -f $dir.Name, $size, $zipSize, $files
}
