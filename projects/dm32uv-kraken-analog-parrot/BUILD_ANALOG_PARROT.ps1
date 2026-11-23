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
    & $exe @arguments 2>&1 | ForEach-Object {
       $line=[string]$_
       $line | Tee-Object -FilePath $Log -Append
    }
    $code=$LASTEXITCODE
    Write-Log "EXIT CODE: $code"
    if($code -ne 0){throw "$exe failed with code $code"}
}
function Fetch([string]$relative,[string]$dest){
    if(-not(Test-Path $dest)){
        $folder=Split-Path -Parent $dest
        New-Item -ItemType Directory -Force -Path $folder | Out-Null
        Write-Log "DOWNLOAD: $relative"
        Invoke-WebRequest ($Project+$relative) -OutFile $dest -UseBasicParsing -ErrorAction Stop
        if((Get-Item $dest).Length -lt 200){throw "Download too small: $dest"}
    }else{Write-Log "REUSING: $dest"}
}
function CommandPath([string]$exe){
    $c=Get-Command $exe -ErrorAction SilentlyContinue
    if($c){return $c.Source}
    throw "Required tool not found: $exe"
}
$state='STARTED'
try{
    Write-Log "WRCX212 KRAKEN ANALOG PARROT — isolated build controller"
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
    $unixRoot='/mnt/c/Radtel/WRCX212_KRAKEN_ANALOG_PARROT/workspace'
    $cmd=@'
set -euo pipefail
cd /mnt/c/Radtel/WRCX212_KRAKEN_ANALOG_PARROT/workspace/dm32uv-kraken
python3 tools/gen_menu_hosts.py
python3 tools/gen_manifest.py
python3 tools/gen_claims.py
if ! command -v docker >/dev/null 2>&1; then
  echo 'BLOCKED: Docker unavailable in WSL'; exit 16
fi
if ! docker info >/dev/null 2>&1; then
  echo 'BLOCKED: Docker daemon unavailable. Enable Docker Desktop WSL integration.'; exit 17
fi
if ! docker image inspect dm32uv-re-pipeline:latest >/dev/null 2>&1; then
  cd ../dm32uv-re/docker
  docker compose build
  cd ../../dm32uv-kraken
fi
tools/build.sh
'@
    Write-Log "Running stock Kraken generators, Docker build and full QEMU tests in WSL"
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

