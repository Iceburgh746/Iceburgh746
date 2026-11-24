# WRCX212 DM-32UV Kraken Analog Parrot: isolated, read/test/build only
# Does not flash the radio. Keeps original source, known-good firmware and backups.
# Run from an ordinary 64-bit PowerShell window, with WSL2 Ubuntu + Docker Desktop.
$ErrorActionPreference='Stop'
$Root='C:\Radtel\WRCX212_KRAKEN_ANALOG_PARROT'
$Project='https://raw.githubusercontent.com/Iceburgh746/Iceburgh746/main/projects/dm32uv-kraken-analog-parrot/'
$LogDir=Join-Path $Root 'Logs'
New-Item -Path $LogDir -ItemType Directory -Force | Out-Null
$Stamp=Get-Date -Format 'yyyyMMdd_HHmmss'
$Log=Join-Path $LogDir "Build_$Stamp.log"
function Write-Log([string]$m){
    $line="[$(Get-Date -Format HH:mm:ss)] $m"
    $line | Tee-Object -FilePath $Log -Append
}
function Native([string]$exe,[string[]]$arguments){
    Write-Log "RUN: $exe $($arguments -join ' ')"
    # PS 5.1 can raise NativeCommandError for stderr progress under Stop;
    # permit native stderr while still checking the real exit code.
    $prior=$ErrorActionPreference
    try {
        $ErrorActionPreference='Continue'
        & $exe @arguments 2>&1 | ForEach-Object {
            $line=[string]$_
            $line | Tee-Object -FilePath $Log -Append
        }
        $code=$LASTEXITCODE
    }finally{$ErrorActionPreference=$prior}
    Write-Log "EXIT CODE: $code"
    if($code -ne 0){throw "$exe failed with code $code"}
}
function Fetch([string]$relative,[string]$dest){
    if($true){
        $folder=Split-Path -Parent $dest
        New-Item -ItemType Directory -Force -Path $folder | Out-Null
        Write-Log "DOWNLOAD: $relative"
        Invoke-WebRequest ($Project+$relative) -OutFile $dest -UseBasicParsing -ErrorAction Stop
        if((Get-Item $dest).Length -lt 200){throw "Download too small: $dest"}
    }
}

function DockerCliPath {
    $found = Get-Command docker.exe -ErrorAction SilentlyContinue
    if ($found) { return $found.Source }
    foreach ($candidate in @(
        (Join-Path $env:ProgramFiles 'Docker\Docker\resources\bin\docker.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\DockerDesktop\resources\bin\docker.exe')
    )) {
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }
    throw 'Docker Desktop CLI missing'
}
function DockerIntegrated {
    param([string]$wsl)
    $old = $ErrorActionPreference
    try {
        $ErrorActionPreference='Continue'
        & $wsl -d Ubuntu -- bash -lc 'command -v docker >/dev/null 2>&1 && docker info >/dev/null 2>&1' 2>&1 | Out-Null
        return ($LASTEXITCODE -eq 0)
    } finally { $ErrorActionPreference=$old }
}
function WaitForIntegration {
    param([string]$wsl,[int]$attempts)
    for ($j=0; $j -lt $attempts; $j++) {
        if (DockerIntegrated $wsl) {
            Write-Log 'PASS: Docker daemon reachable inside WSL Ubuntu'
            return $true
        }
        if ($j -eq 0 -or (($j+1) % 10) -eq 0) { Write-Log "Docker WSL probe $($j+1)/$attempts" }
        Start-Sleep -Seconds 3
    }
    return $false
}
function UpdateDockerSetting {
    param($obj,[string]$name,$value)
    $keys = @($obj.PSObject.Properties | Where-Object { $_.Name -ieq $name })
    if ($keys.Count -gt 1) { throw "Ambiguous Docker setting: $name" }
    if ($keys.Count -eq 1) { $keys[0].Value = $value }
    else { $obj | Add-Member -NotePropertyName $name -NotePropertyValue $value }
}
function FixDockerIntegration {
    param([string]$wsl)
    $docker = DockerCliPath
    $settingsFile = Join-Path $env:APPDATA 'Docker\settings-store.json'
    if (-not (Test-Path -LiteralPath $settingsFile)) {
        throw "Docker settings-store.json not found: $settingsFile"
    }
    # Stop only if no unrelated containers are running.
    $old = $ErrorActionPreference
    try {
        $ErrorActionPreference='Continue'
        $running = @(& $docker --context desktop-linux ps --format '{{.ID}}' 2>$null |
            Where-Object { $_ -match '^[0-9a-f]{8,}
    $c=Get-Command $exe -ErrorAction SilentlyContinue
    if($c){return $c.Source}
    throw "Required tool not found: $exe"
}
$state='STARTED'
try{
    Write-Log "WRCX212 KRAKEN ANALOG PARROT V5 - one-shot Docker WSL integration repair and build"
    $git=CommandPath 'git.exe'
    $wsl=CommandPath 'wsl.exe'
    $python=Get-Command 'py.exe' -ErrorAction SilentlyContinue
    $pyArgs=@('-3')
    if($python){$py=$python.Source}
    else{$py=CommandPath 'python.exe';$pyArgs=@()}
    $stage=Join-Path $Root 'workspace'
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    $repo=Join-Path $stage 'dm32uv-kraken'
    $re=Join-Path $stage 'dm32uv-re'
    if(!(Test-Path (Join-Path $repo '.git'))){
        Native $git @('clone','https://github.com/awkaplan/dm32uv-kraken.git',$repo)
    }else{Write-Log "Original Kraken checkout already staged. No automatic reset."}
    if(!(Test-Path (Join-Path $re '.git'))){
        Native $git @('clone','--recurse-submodules','https://github.com/awkaplan/dm32uv-re.git',$re)
    }else{Write-Log "Original reverse-engineering checkout already staged. No automatic reset."}
    $toolDir=Join-Path $Root 'patcher'
    $apply=Join-Path $toolDir 'apply_analog_parrot.py'
    $overlay=Join-Path $toolDir 'overlay\krk_parrot.c'
    Fetch 'apply_analog_parrot.py' $apply
    Fetch 'overlay/krk_parrot.c' $overlay
    $newsrc=Get-Content (Join-Path $repo 'patches\kraken_ui\krk_parrot.c') -Raw
    if($newsrc -notmatch 'WRCX212 Analog Parrot'){
        Write-Log 'Applying source overlay with backup verification'
        Native $py ($pyArgs+@($apply,$repo))
    }else{
        Write-Log 'Analog Parrot source already patched; no repeated modifications'
    }
    # Start Docker Desktop if present, then prove its daemon is available to WSL Ubuntu.
    # Never modify WSL configuration or install a second Docker daemon automatically.
    $dockerDesktop = @(
        (Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\DockerDesktop\Docker Desktop.exe')
    ) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if ($dockerDesktop) {
        $desktopProcess = Get-Process -Name 'Docker Desktop' -ErrorAction SilentlyContinue
        if (-not $desktopProcess) {
            Write-Log "Docker Desktop installed, starting application"
            Start-Process -FilePath $dockerDesktop -ErrorAction Stop | Out-Null
        } else {
            Write-Log "Docker Desktop process already running"
        }
    } else {
        Write-Log "Docker Desktop executable not found in standard Windows locations"
    }

    Write-Log "Checking Docker Desktop Ubuntu WSL integration"
    if (-not (WaitForIntegration $wsl 6)) {
        Write-Log "Ubuntu integration unavailable: attempting backed-up configuration repair"
        FixDockerIntegration $wsl
    }
    if (-not (DockerIntegrated $wsl)) { throw 'Docker Ubuntu integration still unavailable' }
    $cmd = @(
        'set -euo pipefail',
        'cd /mnt/c/Radtel/WRCX212_KRAKEN_ANALOG_PARROT/workspace/dm32uv-kraken',
        'python3 tools/gen_menu_hosts.py',
        'python3 tools/gen_manifest.py',
        'python3 tools/gen_claims.py',
        'if ! command -v docker >/dev/null 2>&1; then echo BLOCKED_DOCKER_MISSING; exit 16; fi',
        'if ! docker info >/dev/null 2>&1; then echo BLOCKED_DOCKER_DAEMON; exit 17; fi',
        'if ! docker image inspect dm32uv-re-pipeline:latest >/dev/null 2>&1; then cd ../dm32uv-re/docker; docker compose build; cd ../../dm32uv-kraken; fi',
        'bash tools/build.sh'
    ) -join "`n"
    Write-Log "Running stock Kraken generators, Docker build and full QEMU tests in WSL"
    # bash -lc receives LF-only newlines. CRLF changes "pipefail" to "pipefail\r".
    if ($cmd.Contains([char]13)) { throw 'WSL script contains CR characters' }
    Native $wsl @('-d','Ubuntu','--','bash','-lc',$cmd)
    $state='BUILT_QEMU_TESTED_NOT_HARDWARE_TESTED'
    Write-Log "SUCCESS: SOURCE COMPILED and QEMU regression suite returned exit 0. NO RADIO FLASHED."
}catch{
    $state='BLOCKED_UNTESTED'
    Write-Log "BUILD STOPPED SAFELY: $($_.Exception.Message)"
}finally{
    $Report=Join-Path $LogDir "Report_$Stamp.json"
    $result=[ordered]@{
        status=$state
        log=$Log
        source=(Join-Path $Root 'workspace\dm32uv-kraken')
        radio_flashed=$false
        hardware_tested=$false
        time=(Get-Date).ToString('o')
    }
    $result | ConvertTo-Json -Depth 4 | Set-Content -Path $Report -Encoding UTF8
    Write-Log "Report: $Report"
    Write-Log "FINISHED: $state"
    try{[console]::Beep(880,350)}catch{}
}

 })
        $psExit = $LASTEXITCODE
    } finally { $ErrorActionPreference=$old }
    if ($psExit -ne 0) {
        throw 'Windows Docker engine not ready. Cannot safely verify other containers.'
    }
    if ($running.Count -gt 0) {
        throw 'Docker has running containers; refusing to disrupt other projects.'
    }
    $settingsBackupRoot = Join-Path $Root 'Docker_Settings_Backups'
    New-Item -ItemType Directory -Force -Path $settingsBackupRoot | Out-Null
    $backup = Join-Path $settingsBackupRoot "settings-store_$Stamp.json"
    Copy-Item -LiteralPath $settingsFile -Destination $backup -ErrorAction Stop
    if ((Get-FileHash $settingsFile -Algorithm SHA256).Hash -ne
        (Get-FileHash $backup -Algorithm SHA256).Hash) {
        throw 'Docker settings backup hash failed'
    }
    Write-Log "Verified Docker settings backup: $backup"
    Native $docker @('desktop','stop')
    $config = Get-Content -LiteralPath $settingsFile -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($null -eq $config) { throw 'Docker settings invalid JSON' }
    $property = @($config.PSObject.Properties | Where-Object { $_.Name -ieq 'IntegratedWslDistros' })
    $names = @()
    if ($property.Count -eq 1) {
        $names = @($property[0].Value | Where-Object { $_ -is [string] -and $_ })
    }
    if ($property.Count -gt 1) { throw 'Ambiguous integratedWslDistros setting' }
    if (-not @($names | Where-Object { $_ -ieq 'Ubuntu' }).Count) { $names += 'Ubuntu' }
    UpdateDockerSetting $config 'IntegratedWslDistros' @($names)
    UpdateDockerSetting $config 'EnableIntegrationWithDefaultWslDistro' $true
    $engine = @($config.PSObject.Properties | Where-Object { $_.Name -ieq 'WslEngineEnabled' })
    if ($engine.Count -eq 1) { UpdateDockerSetting $config 'WslEngineEnabled' $true }
    $tmp = $settingsFile + '.wrcx212.tmp'
    try {
        $json = $config | ConvertTo-Json -Depth 100
        [System.IO.File]::WriteAllText($tmp,$json,(New-Object System.Text.UTF8Encoding($false)))
        $verify = Get-Content -LiteralPath $tmp -Raw -Encoding UTF8 | ConvertFrom-Json
        if (-not @($verify.IntegratedWslDistros | Where-Object { $_ -ieq 'Ubuntu' }).Count) {
            throw 'Docker settings re-read validation failed'
        }
        Move-Item -LiteralPath $tmp -Destination $settingsFile -Force
    } finally {
        if (Test-Path $tmp) { Remove-Item -LiteralPath $tmp -Force }
    }
    Write-Log 'Ubuntu WSL integration enabled in backed-up Docker settings'
    Native $docker @('desktop','start')
    if (-not (WaitForIntegration $wsl 60)) {
        throw 'Docker Desktop did not activate WSL integration. Original JSON backup preserved.'
    }
}

function CommandPath([string]$exe){
    $c=Get-Command $exe -ErrorAction SilentlyContinue
    if($c){return $c.Source}
    throw "Required tool not found: $exe"
}
$state='STARTED'
try{
    Write-Log "WRCX212 KRAKEN ANALOG PARROT V4 - Docker engine startup and validation"
    $git=CommandPath 'git.exe'
    $wsl=CommandPath 'wsl.exe'
    $python=Get-Command 'py.exe' -ErrorAction SilentlyContinue
    $pyArgs=@('-3')
    if($python){$py=$python.Source}
    else{$py=CommandPath 'python.exe';$pyArgs=@()}
    $stage=Join-Path $Root 'workspace'
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    $repo=Join-Path $stage 'dm32uv-kraken'
    $re=Join-Path $stage 'dm32uv-re'
    if(!(Test-Path (Join-Path $repo '.git'))){
        Native $git @('clone','https://github.com/awkaplan/dm32uv-kraken.git',$repo)
    }else{Write-Log "Original Kraken checkout already staged. No automatic reset."}
    if(!(Test-Path (Join-Path $re '.git'))){
        Native $git @('clone','--recurse-submodules','https://github.com/awkaplan/dm32uv-re.git',$re)
    }else{Write-Log "Original reverse-engineering checkout already staged. No automatic reset."}
    $toolDir=Join-Path $Root 'patcher'
    $apply=Join-Path $toolDir 'apply_analog_parrot.py'
    $overlay=Join-Path $toolDir 'overlay\krk_parrot.c'
    Fetch 'apply_analog_parrot.py' $apply
    Fetch 'overlay/krk_parrot.c' $overlay
    $newsrc=Get-Content (Join-Path $repo 'patches\kraken_ui\krk_parrot.c') -Raw
    if($newsrc -notmatch 'WRCX212 Analog Parrot'){
        Write-Log 'Applying source overlay with backup verification'
        Native $py ($pyArgs+@($apply,$repo))
    }else{
        Write-Log 'Analog Parrot source already patched; no repeated modifications'
    }
    # Start Docker Desktop if present, then prove its daemon is available to WSL Ubuntu.
    # Never modify WSL configuration or install a second Docker daemon automatically.
    $dockerDesktop = @(
        (Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\DockerDesktop\Docker Desktop.exe')
    ) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if ($dockerDesktop) {
        $desktopProcess = Get-Process -Name 'Docker Desktop' -ErrorAction SilentlyContinue
        if (-not $desktopProcess) {
            Write-Log "Docker Desktop installed, starting application"
            Start-Process -FilePath $dockerDesktop -ErrorAction Stop | Out-Null
        } else {
            Write-Log "Docker Desktop process already running"
        }
    } else {
        Write-Log "Docker Desktop executable not found in standard Windows locations"
    }

    Write-Log "Verifying Docker engine connection from WSL Ubuntu"
    $dockerReady = $false
    for ($probe = 1; $probe -le 45; $probe++) {
        $prior = $ErrorActionPreference
        try {
            $ErrorActionPreference = 'Continue'
            & $wsl -d Ubuntu -- bash -lc 'docker info >/dev/null 2>&1'
            $probeCode = $LASTEXITCODE
        } finally { $ErrorActionPreference = $prior }
        if ($probeCode -eq 0) {
            $dockerReady = $true
            Write-Log "PASS: Docker engine accessible from WSL Ubuntu"
            break
        }
        if ($probe -eq 1 -or ($probe % 10) -eq 0) {
            Write-Log "Docker engine unavailable from WSL (check $probe)"
        }
        Start-Sleep -Seconds 3
    }
    if (-not $dockerReady) {
        $diag = (& $wsl -d Ubuntu -- bash -lc 'docker context ls 2>&1; docker version 2>&1' | Out-String)
        Write-Log $diag
        throw "DOCKER_WSL_UNAVAILABLE: Start Docker Desktop; enable Settings > Resources > WSL Integration > Ubuntu. The Docker daemon is not reachable in Ubuntu."
    }
    $cmd = @(
        'set -euo pipefail',
        'cd /mnt/c/Radtel/WRCX212_KRAKEN_ANALOG_PARROT/workspace/dm32uv-kraken',
        'python3 tools/gen_menu_hosts.py',
        'python3 tools/gen_manifest.py',
        'python3 tools/gen_claims.py',
        'if ! command -v docker >/dev/null 2>&1; then echo BLOCKED_DOCKER_MISSING; exit 16; fi',
        'if ! docker info >/dev/null 2>&1; then echo BLOCKED_DOCKER_DAEMON; exit 17; fi',
        'if ! docker image inspect dm32uv-re-pipeline:latest >/dev/null 2>&1; then cd ../dm32uv-re/docker; docker compose build; cd ../../dm32uv-kraken; fi',
        'bash tools/build.sh'
    ) -join "`n"
    Write-Log "Running stock Kraken generators, Docker build and full QEMU tests in WSL"
    # bash -lc receives LF-only newlines. CRLF changes "pipefail" to "pipefail\r".
    if ($cmd.Contains([char]13)) { throw 'WSL script contains CR characters' }
    Native $wsl @('-d','Ubuntu','--','bash','-lc',$cmd)
    $state='BUILT_QEMU_TESTED_NOT_HARDWARE_TESTED'
    Write-Log "SUCCESS: SOURCE COMPILED and QEMU regression suite returned exit 0. NO RADIO FLASHED."
}catch{
    $state='BLOCKED_UNTESTED'
    Write-Log "BUILD STOPPED SAFELY: $($_.Exception.Message)"
}finally{
    $Report=Join-Path $LogDir "Report_$Stamp.json"
    $result=[ordered]@{
        status=$state
        log=$Log
        source=(Join-Path $Root 'workspace\dm32uv-kraken')
        radio_flashed=$false
        hardware_tested=$false
        time=(Get-Date).ToString('o')
    }
    $result | ConvertTo-Json -Depth 4 | Set-Content -Path $Report -Encoding UTF8
    Write-Log "Report: $Report"
    Write-Log "FINISHED: $state"
    try{[console]::Beep(880,350)}catch{}
}

