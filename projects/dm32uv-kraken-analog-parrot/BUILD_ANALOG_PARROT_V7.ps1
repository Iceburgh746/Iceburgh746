# WRCX212 Analog Parrot V7: Docker Ubuntu repair + existing Kraken build V4
# Windows PowerShell 5.1; fail-closed; source backups; no radio flashing.
$ErrorActionPreference = 'Stop'
$root = 'C:\Radtel\WRCX212_KRAKEN_ANALOG_PARROT'
$logs = Join-Path $root 'Logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$log = Join-Path $logs "V7_$stamp.log"
$status = 'STARTED'
function Log([string]$message) {
    "[$(Get-Date -Format HH:mm:ss)] $message" | Tee-Object -FilePath $log -Append
}
function Native([string]$exe,[string[]]$argv) {
    Log "RUN: $exe $($argv -join ' ')"
    $prior = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & $exe @argv 2>&1 | ForEach-Object { [string]$_ | Tee-Object -FilePath $log -Append }
        $exit = $LASTEXITCODE
    } finally { $ErrorActionPreference = $prior }
    Log "EXIT CODE: $exit"
    if ($exit -ne 0) { throw "COMMAND_FAILED exit=$exit exe=$exe" }
}
function Probe {
    $prior = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & $wsl -d Ubuntu -- bash -lc 'command -v docker >/dev/null 2>&1 && docker info >/dev/null 2>&1' 2>&1 | Out-Null
        return ($LASTEXITCODE -eq 0)
    } finally { $ErrorActionPreference = $prior }
}
function FindDocker {
    $c = Get-Command docker.exe -ErrorAction SilentlyContinue
    if ($c) { return $c.Source }
    $p = Join-Path $env:ProgramFiles 'Docker\Docker\resources\bin\docker.exe'
    if (Test-Path $p) { return $p }
    throw 'Windows Docker CLI missing'
}
try {
    Log 'V7 START: repair Ubuntu WSL integration and resume existing Kraken build'
    $wc = Get-Command wsl.exe -ErrorAction Stop
    $wsl = $wc.Source
    $desktopExe = Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe'
    if (-not (Test-Path $desktopExe)) { throw 'Docker Desktop application missing' }
    if (-not (Get-Process -Name 'Docker Desktop' -ErrorAction SilentlyContinue)) {
        Start-Process -FilePath $desktopExe | Out-Null
        Log 'Launched Docker Desktop'
    }
    $ready = $false
    for ($i = 0; $i -lt 15; $i++) {
        if (Probe) { $ready = $true; break }
        Start-Sleep -Seconds 3
    }
    if (-not $ready) {
        Log 'Docker not integrated with Ubuntu; repairing Docker Desktop settings'
        $docker = FindDocker
        $prior = $ErrorActionPreference
        try {
            $ErrorActionPreference = 'Continue'
            $running = @(& $docker --context desktop-linux ps --format '{{.ID}}' 2>$null |
                Where-Object { $_ -match '^[0-9a-f]{8,}$' })
            $rc = $LASTEXITCODE
        } finally { $ErrorActionPreference = $prior }
        if ($rc -ne 0) { throw 'Windows Docker daemon unavailable; refusing settings edit' }
        if ($running.Count) { throw 'Running Docker containers found; refusing to interrupt them' }
        $settings = Join-Path $env:APPDATA 'Docker\settings-store.json'
        if (-not (Test-Path $settings)) { throw 'Docker settings-store.json missing' }
        $obj = Get-Content -LiteralPath $settings -Raw -Encoding UTF8 | ConvertFrom-Json
        $prop = @($obj.PSObject.Properties | Where-Object { $_.Name -ieq 'IntegratedWslDistros' })
        if ($prop.Count -gt 1) { throw 'Docker integration settings ambiguous' }
        $backupDir = Join-Path $root 'Docker_Settings_Backups'
        New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
        $backup = Join-Path $backupDir "settings-store_$stamp.json"
        Copy-Item -LiteralPath $settings -Destination $backup -ErrorAction Stop
        if ((Get-FileHash $settings -Algorithm SHA256).Hash -ne
            (Get-FileHash $backup -Algorithm SHA256).Hash) { throw 'Backup hash mismatch' }
        Log "Verified settings backup: $backup"
        Native $docker @('desktop','stop')
        $obj = Get-Content -LiteralPath $settings -Raw -Encoding UTF8 | ConvertFrom-Json
        $prop = @($obj.PSObject.Properties | Where-Object { $_.Name -ieq 'IntegratedWslDistros' })
        if ($prop.Count -gt 1) { throw 'Docker integration property ambiguous' }
        $distros = @()
        if ($prop.Count -eq 1 -and $null -ne $prop[0].Value) {
            $distros = @($prop[0].Value | Where-Object { $_ -is [string] -and $_.Length })
        }
        if (-not @($distros | Where-Object { $_ -ieq 'Ubuntu' }).Count) { $distros += 'Ubuntu' }
        if ($prop.Count -eq 1) {
            $prop[0].Value = [string[]]$distros
        } else {
            $obj | Add-Member -NotePropertyName 'integratedWslDistros' -NotePropertyValue ([string[]]$distros)
        }
        $default = @($obj.PSObject.Properties | Where-Object { $_.Name -ieq 'enableIntegrationWithDefaultWslDistro' })
        if ($default.Count -eq 1) {
            $default[0].Value = $true
        } elseif ($default.Count -eq 0) {
            $obj | Add-Member -NotePropertyName 'enableIntegrationWithDefaultWslDistro' -NotePropertyValue $true
        } else { throw 'Ambiguous default distro integration setting' }
        $temp = $settings + '.wrcx212.tmp'
        try {
            $json = $obj | ConvertTo-Json -Depth 100
            [System.IO.File]::WriteAllText($temp,$json,(New-Object System.Text.UTF8Encoding($false)))
            $checked = Get-Content $temp -Raw -Encoding UTF8 | ConvertFrom-Json
            if (-not @($checked.IntegratedWslDistros | Where-Object { $_ -ieq 'Ubuntu' }).Count) {
                throw 'Edited Docker settings failed verification'
            }
            Move-Item -LiteralPath $temp -Destination $settings -Force
        } finally { if (Test-Path $temp) { Remove-Item $temp -Force } }
        Log 'Ubuntu enabled in Docker integration settings'
        Native $docker @('desktop','start')
        for ($i = 0; $i -lt 60; $i++) {
            if (Probe) { $ready = $true; break }
            if (($i % 10) -eq 0) { Log "Checking Ubuntu Docker connection: $i" }
            Start-Sleep -Seconds 3
        }
    }
    if (-not $ready) { throw 'Docker Ubuntu integration still unavailable; no build executed' }
    Log 'PASS: Docker integrated with Ubuntu'
    $base = 'https://raw.githubusercontent.com/Iceburgh746/Iceburgh746/main/projects/dm32uv-kraken-analog-parrot/'
    $v4 = Join-Path $root 'BUILD_ANALOG_PARROT_V4.ps1'
    Invoke-WebRequest -Uri ($base + 'BUILD_ANALOG_PARROT_V4.ps1') -OutFile $v4 -UseBasicParsing
    $tokens = $null
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($v4,[ref]$tokens,[ref]$errors) | Out-Null
    if ($errors.Count) { throw ('V4 build script parse failure: ' + ($errors | Out-String)) }
    Log 'Running existing Kraken build controller and QEMU suite'
    $buildStarted = Get-Date
    Native 'powershell.exe' @('-NoProfile','-ExecutionPolicy','Bypass','-File',$v4)
    # V4 returns a report and can exit zero even when its own internal build is blocked.
    $reports = @(Get-ChildItem -Path $logs -Filter 'Report_*.json' |
        Where-Object { $_.LastWriteTime -ge $buildStarted } |
        Sort-Object LastWriteTime -Descending)
    if (-not $reports.Count) { throw 'Kraken build report missing' }
    $report = Get-Content -LiteralPath $reports[0].FullName -Raw | ConvertFrom-Json
    if ($report.status -ne 'BUILT_QEMU_TESTED_NOT_HARDWARE_TESTED') {
        throw ('Kraken build not proven: ' + $report.status)
    }
    $status = 'BUILD_AND_QEMU_PASSED_NOT_HARDWARE_TESTED'
    Log 'PASS: Kraken build and QEMU complete. NO RADIO FLASHED.'
} catch {
    $status = 'BLOCKED_NO_FLASH'
    Log ('STOPPED: ' + $_.Exception.Message)
} finally {
    $result = [ordered]@{
        status = $status
        log = $log
        radio_flashed = $false
        hardware_tested = $false
        completed = (Get-Date).ToString('o')
    }
    $reportPath = Join-Path $logs "V7_Report_$stamp.json"
    $result | ConvertTo-Json | Set-Content -Path $reportPath -Encoding UTF8
    Log ('REPORT: ' + $reportPath)
    Log ('FINISHED: ' + $status)
}
