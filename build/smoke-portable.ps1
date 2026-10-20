param([string]$Exe = "release/LiteMon-portable-win-x64/LiteMon.exe")
$ErrorActionPreference = "Stop"
Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class SW {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
}
'@

Get-Process LiteMon -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

$sw = [Diagnostics.Stopwatch]::StartNew()
Start-Process -FilePath $Exe
$found = $false

for ($i = 0; $i -lt 150; $i++) {
    Start-Sleep -Milliseconds 200
    $proc = Get-Process LiteMon -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $proc) { continue }

    $script:hwnd = [IntPtr]::Zero
    $target = $proc.Id
    [SW]::EnumWindows({ param($h, $l)
        $pp = 0
        [SW]::GetWindowThreadProcessId($h, [ref]$pp) | Out-Null
        if ($pp -eq $target) {
            $sb = New-Object System.Text.StringBuilder 256
            [SW]::GetWindowText($h, $sb, 256) | Out-Null
            if ($sb.ToString() -eq "LiteMon") { $script:hwnd = $h; return $false }
        }
        return $true
    }, [IntPtr]::Zero) | Out-Null

    if ($script:hwnd -ne [IntPtr]::Zero) { $found = $true; break }
}
$sw.Stop()

if (-not $found) { Write-Error "window not found within 30s"; exit 1 }

Start-Sleep -Seconds 6
$proc = Get-Process LiteMon | Select-Object -First 1
"window shown in {0:N1} s" -f $sw.Elapsed.TotalSeconds
"pid={0} ws={1:N0}MB private={2:N0}MB" -f $proc.Id, ($proc.WorkingSet64/1MB), ($proc.PrivateMemorySize64/1MB)
& "$PSScriptRoot/screenshot.ps1" -Out "artifacts/portable.png" -ProcessName "LiteMon"
