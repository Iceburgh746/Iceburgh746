# WRCX212 Windows System Optimizer v1.0
# Windows 10/11 x64; Windows PowerShell 5.1+; safe, evidence-based maintenance.
# No automated reboots, no registry cleaning, no Defender/service disabling.
# Modes: Audit (default), Optimize (opt-in), Repair (opt-in), Menu.
[CmdletBinding()]
param(
  [ValidateSet('Menu','Audit','Optimize','Repair')]
  [string]$Mode = 'Menu'
)
$ErrorActionPreference = 'Stop'
$Root = 'C:\Radtel\WRCX212_System_Optimizer'
$Logs = Join-Path $Root 'Logs'
New-Item -Path $Logs -ItemType Directory -Force | Out-Null
$Stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$LogPath = Join-Path $Logs "Optimizer_$Stamp.log"
$ReportPath = Join-Path $Logs "Optimizer_$Stamp.json"
$script:Report = [ordered]@{
  app = 'WRCX212 Windows System Optimizer'
  version = '1.0'
  timestamp = (Get-Date).ToString('o')
  requested_mode = $Mode
  performed = @()
  warnings = @()
  diagnostics = [ordered]@{}
  reboot_initiated = $false
}
function Say([string]$Message) {
  $line = "[$(Get-Date -Format 'HH:mm:ss')] $Message"
  Write-Host $line
  Add-Content -Path $LogPath -Value $line -Encoding UTF8
}
function Note([string]$Message) {
  $script:Report.warnings += $Message
  Say "WARNING: $Message"
}
function Do-Record([string]$Message) {
  $script:Report.performed += $Message
  Say "OK: $Message"
}
function Is-Admin {
  $id = [Security.Principal.WindowsIdentity]::GetCurrent()
  $principal = New-Object Security.Principal.WindowsPrincipal($id)
  return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}
function Capture-Command([string]$Exe, [string[]]$Arg) {
  Say "Running $Exe $($Arg -join ' ')"
  & $Exe @Arg 2>&1 | ForEach-Object { Say ([string]$_) }
  $exit = $LASTEXITCODE
  if ($exit -ne 0) { Note "$Exe returned exit code $exit" }
  return $exit
}
function Audit {
  Say 'Starting read-only system audit'
  $os = Get-CimInstance Win32_OperatingSystem
  $cpu = Get-CimInstance Win32_Processor
  $ram = Get-CimInstance Win32_PhysicalMemory
  $disk = Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=3'
  $total = ($ram | Measure-Object Capacity -Sum).Sum
  $script:Report.diagnostics.os = $os.Caption
  $script:Report.diagnostics.build = $os.BuildNumber
  $script:Report.diagnostics.architecture = $os.OSArchitecture
  $script:Report.diagnostics.processor = @($cpu | ForEach-Object { $_.Name })
  $script:Report.diagnostics.installed_ram_gb = [math]::Round($total / 1GB,2)
  $script:Report.diagnostics.visible_ram_gb = [math]::Round($os.TotalVisibleMemorySize / 1MB,2)
  $script:Report.diagnostics.logical_processors = @($cpu | Measure-Object NumberOfLogicalProcessors -Sum)[0].Sum
  $script:Report.diagnostics.drives = @($disk | ForEach-Object {
    [ordered]@{ name=$_.DeviceID; size_gb=[math]::Round($_.Size/1GB,2);
      free_gb=[math]::Round($_.FreeSpace/1GB,2) }
  })
  $script:Report.diagnostics.admin = Is-Admin
  $bcd = @(& bcdedit.exe /enum '{current}' 2>&1 | ForEach-Object { [string]$_ })
  $script:Report.diagnostics.bcd_memory_restriction = @($bcd | Where-Object { $_ -match '^\s*(truncatememory|removememory)\s+' })
  $script:Report.diagnostics.bcd_numproc = @($bcd | Where-Object { $_ -match '^\s*numproc\s+' })
  $script:Report.diagnostics.bcd_read_success = ($LASTEXITCODE -eq 0)
  if (!$script:Report.diagnostics.bcd_read_success) { Note 'Boot settings could not be read' }
  try {
    $pd = Get-PhysicalDisk -ErrorAction Stop
    $script:Report.diagnostics.physical_drives = @($pd | ForEach-Object {
      [ordered]@{ name=$_.FriendlyName; media=$_.MediaType; health=$_.HealthStatus }
    })
  } catch { Note "Physical disk inventory unavailable: $_" }
  try {
    $sense = Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy' -ErrorAction Stop
    $script:Report.diagnostics.storage_sense_enabled = ($sense.'01' -eq 1)
  } catch {
    $script:Report.diagnostics.storage_sense_enabled = $false
  }
  try {
    $svc = Get-Service WinDefend -ErrorAction Stop
    $script:Report.diagnostics.defender_service_status = [string]$svc.Status
  } catch { Note 'Microsoft Defender service not found or not readable' }
  try {
    $pending = @(& schtasks.exe /Query /TN '\Microsoft\Windows\Defrag\ScheduledDefrag' /FO LIST 2>&1)
    $script:Report.diagnostics.drive_optimization_task = ($pending | Out-String).Trim()
  } catch { Note 'Drive optimization scheduled task could not be queried' }
  Say "RAM: $($script:Report.diagnostics.installed_ram_gb) GB installed, $($script:Report.diagnostics.visible_ram_gb) GB Windows-visible"
  if (@($script:Report.diagnostics.bcd_memory_restriction).Count) {
    Note 'BCD memory restriction detected: review before modifying'
  }
  Do-Record 'Read-only Windows audit completed'
}
function Backup-Settings {
  $backup = Join-Path $Root "Backups\$Stamp"
  New-Item -Path $backup -ItemType Directory -Force | Out-Null
  try {
    & reg.exe export 'HKCU\Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy' (Join-Path $backup 'StorageSense.reg') /y 2>&1 | ForEach-Object { Say ([string]$_) }
    if ($LASTEXITCODE -ne 0) { Note 'Storage Sense configuration did not exist or could not be exported' }
  } catch { Note "Storage Sense export failed: $_" }
  try {
    & bcdedit.exe /export (Join-Path $backup 'BCD.bak') 2>&1 | ForEach-Object { Say ([string]$_) }
    if ($LASTEXITCODE -ne 0) { Note 'BCD backup unavailable; no boot changes will be attempted' }
  } catch { Note "BCD backup failed: $_" }
  try {
    if (Get-Command Checkpoint-Computer -ErrorAction SilentlyContinue) {
      Checkpoint-Computer -Description "WRCX212_Optimizer_$Stamp" -RestorePointType MODIFY_SETTINGS -ErrorAction Stop
      Do-Record 'System restore point created'
    }
  } catch { Note "Restore point unavailable (no unsafe fallback): $_" }
  Do-Record "Settings backup directory: $backup"
}
function Optimize {
  if (!(Is-Admin)) { throw 'Optimization requires Administrator PowerShell' }
  Audit
  Backup-Settings
  # Storage Sense enabled ONLY. Retention/Downloads/Recycle Bin policies untouched.
  $policy = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy'
  New-Item -Path $policy -Force | Out-Null
  New-ItemProperty -Path $policy -Name '01' -PropertyType DWord -Value 1 -Force | Out-Null
  Do-Record 'Enabled built-in Storage Sense; did not modify Downloads or Recycle Bin retention'
  # Read-only health scan: no image repair unless Repair is deliberately selected.
  $exit = Capture-Command 'DISM.exe' @('/Online','/Cleanup-Image','/ScanHealth')
  if ($exit -eq 0) { Do-Record 'DISM component health scan completed' }
  Do-Record 'Safe optimization completed; no reboot performed'
}
function Repair {
  if (!(Is-Admin)) { throw 'Repair requires Administrator PowerShell' }
  Audit
  Backup-Settings
  Say 'Repair requested: DISM RestoreHealth followed by SFC Scannow'
  $dismExit = Capture-Command 'DISM.exe' @('/Online','/Cleanup-Image','/RestoreHealth')
  if ($dismExit -ne 0) { throw "DISM failed with exit code $dismExit; SFC not started" }
  Do-Record 'DISM RestoreHealth completed successfully'
  $sfcExit = Capture-Command 'sfc.exe' @('/scannow')
  if ($sfcExit -ne 0) { Note "SFC exit code $sfcExit; inspect detailed logs" }
  else { Do-Record 'SFC scan completed successfully' }
}
try {
  if ([IntPtr]::Size -ne 8) { throw 'Requires 64-bit PowerShell' }
  $ver = [Environment]::OSVersion.Version
  if ($ver.Major -lt 10) { throw 'Requires Windows 10 or newer' }
  Say 'WRCX212 System Optimizer v1.0'
  if ($Mode -eq 'Menu') {
    Write-Host ''
    Write-Host '1 = Read-only diagnostic (recommended first)'
    Write-Host '2 = Safe maintenance (Storage Sense + DISM scan)'
    Write-Host '3 = Windows repair (DISM RestoreHealth + SFC)'
    Write-Host 'Q = Exit'
    $choice = Read-Host 'Select 1, 2, 3, or Q'
    switch ($choice.ToUpperInvariant()) {
      '1' { $Mode='Audit' }
      '2' { $Mode='Optimize' }
      '3' { $Mode='Repair' }
      'Q' { Say 'User exited'; exit 0 }
      default { throw "Invalid menu selection: $choice" }
    }
  }
  $script:Report.requested_mode = $Mode
  switch ($Mode) {
    Audit    { Audit }
    Optimize { Optimize }
    Repair   { Repair }
  }
  Say 'Operation finished'
} catch {
  Note "Operation stopped: $_"
  $script:Report.error = [string]$_
} finally {
  $script:Report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
  Say "Report: $ReportPath"
  Say "Log: $LogPath"
  Write-Host ''
  if ($script:Report.error) { Write-Host 'FINISHED WITH ERROR - review the report' -ForegroundColor Yellow }
  else { Write-Host 'FINISHED - no reboot was initiated' -ForegroundColor Green }
}
