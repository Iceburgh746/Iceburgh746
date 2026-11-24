# WRCX212 AUTONOMOUS ENGINEER 1.0
# Default project: DM-32UV Kraken Analog Parrot.
# Never flashes radios. Never edits stock or calibration images.
# No OpenAI conversation-injection API exists; optional authenticated GitHub status reporting.
# Windows PowerShell 5.1 and PowerShell 7, ASCII-only.
param(
    [string]$ProjectRoot = 'C:\Radtel\WRCX212_KRAKEN_ANALOG_PARROT',
    [ValidateRange(1,5)][int]$MaxAttempts = 3,
    [switch]$NoCloud,
    [switch]$NoLocalBuild
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$Repo = 'Iceburgh746/Iceburgh746'
$Workflow = 'wrcx212-analog-parrot-cloud.yml'
$SourceURL = 'https://raw.githubusercontent.com/Iceburgh746/Iceburgh746/main/projects/dm32uv-kraken-analog-parrot/'
$LogDir = Join-Path $ProjectRoot 'Autonomous_Logs'
New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
$Started = Get-Date
$Stamp = $Started.ToString('yyyyMMdd_HHmmss')
$Log = Join-Path $LogDir ('Engineer_' + $Stamp + '.log')
$Report = Join-Path $LogDir ('Engineer_' + $Stamp + '.json')
$State = 'STARTED'
$Stage = 'PREFLIGHT'
$Failures = New-Object System.Collections.ArrayList
$Actions = New-Object System.Collections.ArrayList
$Passes = New-Object System.Collections.ArrayList
$Tools = [ordered]@{}
$CloudURL = 'https://github.com/Iceburgh746/Iceburgh746/actions/workflows/wrcx212-analog-parrot-cloud.yml'
$ReadyForFlash = $false

function Note([string]$Message) {
    $line = '[' + (Get-Date).ToString('HH:mm:ss') + '] ' + $Message
    $line | Tee-Object -FilePath $Log -Append
}
function Mark([string]$Message) {
    [void]$script:Passes.Add($Message)
    Note ('PASS ' + $Message)
}
function RecordAction([string]$Message) {
    [void]$script:Actions.Add($Message)
    Note ('ACTION ' + $Message)
}
function Failure([string]$Category,[string]$Message) {
    [void]$script:Failures.Add([ordered]@{ category=$Category; detail=$Message })
    Note ('BLOCKER ' + $Category + ': ' + $Message)
}
function ToolPath([string[]]$names) {
    foreach ($n in $names) {
        $c = Get-Command $n -ErrorAction SilentlyContinue
        if ($c -and $c.Source) { return $c.Source }
    }
    return $null
}
function InvokeNative([string]$exe,[string[]]$argv,[string]$label) {
    if (-not $exe) { throw ('MISSING_TOOL ' + $label) }
    $outFile = Join-Path $LogDir ('stdout_' + $Stamp + '_' + [guid]::NewGuid().ToString('N') + '.txt')
    $errFile = Join-Path $LogDir ('stderr_' + $Stamp + '_' + [guid]::NewGuid().ToString('N') + '.txt')
    Note ('RUN ' + $label + ': ' + $exe)
    # Image/toolchain builds can take longer than status probes; every step has a bounded maximum.
    $timeoutSeconds = 300
    if ($label -match 'local build|cloud build|watch|workflow') { $timeoutSeconds = 10800 }
    if ($label -match 'auth|workflow runs|dispatch') { $timeoutSeconds = 180 }
    $exitCode = -999
    $proc = $null
    try {
        $proc = Start-Process -FilePath $exe -ArgumentList $argv -PassThru -NoNewWindow -RedirectStandardOutput $outFile -RedirectStandardError $errFile -ErrorAction Stop
        if (-not $proc.WaitForExit($timeoutSeconds * 1000)) {
            try { $proc.Kill() } catch { }
            try { $proc.WaitForExit(5000) | Out-Null } catch { }
            Failure 'PROCESS_TIMEOUT' ('Exceeded ' + $timeoutSeconds + ' seconds: ' + $label)
            $exitCode = 124
        } else {
            $exitCode = $proc.ExitCode
        }
    } catch {
        Failure 'PROCESS_START' ($label + ': ' + $_.Exception.Message)
        throw
    } finally {
        if ($proc) { $proc.Dispose() }
    }
    foreach ($file in @($outFile,$errFile)) {
        if (Test-Path -LiteralPath $file) {
            Get-Content -LiteralPath $file -ErrorAction SilentlyContinue |
                Select-Object -Last 80 | ForEach-Object { Add-Content -LiteralPath $Log -Value $_ }
        }
    }
    Note ('EXIT ' + $label + ': ' + $exitCode)
    return [ordered]@{ code=$exitCode; stdout=$outFile; stderr=$errFile }
}
function CheckCliAuth([string]$gh) {
    if (-not $gh) { return $false }
    $r = InvokeNative $gh @('auth','status') 'GitHub CLI auth'
    return ($r.code -eq 0)
}
function AcquireSource {
    $checkout = Join-Path $ProjectRoot 'workspace\dm32uv-kraken'
    if (-not (Test-Path (Join-Path $checkout '.git'))) {
        throw 'Kraken source checkout missing. Preserve previous files; run existing V4 source installer once.'
    }
    foreach ($name in @('patches\kraken_ui\krk_parrot.c','patches\kraken_ui\krk_periodic.c','patches\kraken_ui\menu_hosts.json')) {
        if (-not (Test-Path (Join-Path $checkout $name))) {
            throw ('Missing expected project file: ' + $name)
        }
    }
    $parrot = Get-Content -LiteralPath (Join-Path $checkout 'patches\kraken_ui\krk_parrot.c') -Raw
    $periodic = Get-Content -LiteralPath (Join-Path $checkout 'patches\kraken_ui\krk_periodic.c') -Raw
    if (-not $parrot.Contains('WRCX212 Analog Parrot') -or -not $periodic.Contains('krk_analog_parrot_bg_tick();')) {
        throw 'Analog Parrot source integration cannot be verified; refusing arbitrary source edits'
    }
    Mark 'Existing Analog Parrot source and background hook verified'
    return $checkout
}
function WslDockerReady([string]$wsl) {
    if (-not $wsl) { return $false }
    $r = InvokeNative $wsl @('-d','Ubuntu','--','bash','-lc','command -v docker >/dev/null 2>&1 && docker info >/dev/null 2>&1') 'WSL Docker probe'
    return ($r.code -eq 0)
}
function BuildLocal([string]$wsl) {
    $shellPath = Join-Path $ProjectRoot ('autonomous_build_' + $Stamp + '.sh')
    $lines = @(
        'set -euo pipefail',
        'cd /mnt/c/Radtel/WRCX212_KRAKEN_ANALOG_PARROT/workspace/dm32uv-kraken',
        'python3 tools/gen_menu_hosts.py',
        'python3 tools/gen_manifest.py',
        'python3 tools/gen_claims.py',
        'if ! docker image inspect dm32uv-re-pipeline:latest >/dev/null 2>&1; then cd ../dm32uv-re/docker; docker compose build; cd ../../dm32uv-kraken; fi',
        'bash tools/build.sh'
    )
    # Script file is LF only. Bash reads its path, avoiding Windows argument quoting.
    [System.IO.File]::WriteAllText($shellPath,([string]::Join([char]10,$lines) + [char]10),(New-Object System.Text.UTF8Encoding($false)))
    $linuxScript = '/mnt/c/Radtel/WRCX212_KRAKEN_ANALOG_PARROT/' + (Split-Path $shellPath -Leaf)
    for ($i=1; $i -le $MaxAttempts; $i++) {
        RecordAction ('Local build attempt ' + $i)
        $r = InvokeNative $wsl @('-d','Ubuntu','--','bash',$linuxScript) ('local build '+$i)
        if ($r.code -eq 0) { Mark 'Full local Kraken build and stock QEMU suite returned exit 0'; return $true }
        # Bounded targeted retry: transient registry/network or QEMU timing, never patch unknown C code.
        $errors = Get-Content -LiteralPath $r.stderr -Raw -ErrorAction SilentlyContinue
        if ($errors -match 'timed out|TLS handshake timeout|connection reset|network is unreachable|Temporary failure in name resolution') {
            Failure 'TRANSIENT_NETWORK' ('Attempt ' + $i + ' returned ' + $r.code)
            Start-Sleep -Seconds (3 * $i)
            continue
        }
        if ($errors -match 'timeout|QEMU|gdb') {
            Failure 'QEMU_OR_TEST_TIMEOUT' ('Attempt ' + $i + ' returned ' + $r.code)
            # Do not silently mark an assertion failure as transient.
        } else {
            Failure 'FIRMWARE_BUILD' ('Attempt ' + $i + ' returned ' + $r.code)
        }
        break
    }
    return $false
}
function CloudBuild([string]$gh) {
    if (-not $gh) {
        Failure 'GITHUB_CLI_MISSING' 'Install/authenticate GitHub CLI for unattended cloud dispatch'
        return $false
    }
    if (-not (CheckCliAuth $gh)) {
        Failure 'GITHUB_AUTH' 'GitHub CLI has no authenticated repository access'
        return $false
    }
    RecordAction 'Triggering autonomous GitHub Actions build and test'
    $r = InvokeNative $gh @('workflow','run',$Workflow,'--repo',$Repo,'--ref','main') 'dispatch cloud build'
    if ($r.code -ne 0) {
        Failure 'CLOUD_DISPATCH' 'GitHub Actions dispatch failed; inspect native stdout/stderr'
        return $false
    }
    Mark 'Cloud build dispatched; checking the resulting GitHub Actions run'
    # Bounded polling; no automatic chat-posting API is available.
    $runId = $null
    for ($probe=0; $probe -lt 18; $probe++) {
        Start-Sleep -Seconds 10
        $listed = InvokeNative $gh @('run','list','--repo',$Repo,'--workflow',$Workflow,'--limit','5','--json','databaseId,status,conclusion,createdAt,url') 'get workflow runs'
        if ($listed.code -ne 0) { continue }
        try {
            $items = @(Get-Content -LiteralPath $listed.stdout -Raw | ConvertFrom-Json)
            $candidate = @($items | Where-Object { [datetime]$_.createdAt -ge $Started.AddMinutes(-1) } |
                Sort-Object createdAt -Descending | Select-Object -First 1)
            if ($candidate.Count -gt 0) {
                $runId = [string]$candidate[0].databaseId
                if ($candidate[0].url) { $script:CloudURL = [string]$candidate[0].url }
                break
            }
        } catch {
            Failure 'CLOUD_RUN_LIST_PARSE' $_.Exception.Message
        }
    }
    if (-not $runId) {
        Failure 'CLOUD_RUN_UNKNOWN' 'Dispatch succeeded but new workflow run was not located'
        return $false
    }
    RecordAction ('Monitoring GitHub Actions run ' + $runId)
    $watch = InvokeNative $gh @('run','watch',$runId,'--repo',$Repo,'--exit-status','--interval','15') 'watch cloud build'
    if ($watch.code -eq 0) {
        Mark 'Cloud workflow completed successfully (host/source and full-build jobs)'
        return $true
    }
    # Safe bounded retry: once only; do not rewrite C source based on untrusted logs.
    for ($retry=1; $retry -le 1; $retry++) {
        $run = InvokeNative $gh @('run','rerun',$runId,'--repo',$Repo,'--failed') 'retry failed GitHub Actions jobs'
        if ($run.code -ne 0) { break }
        RecordAction 'Retried failed cloud jobs once'
        $watch = InvokeNative $gh @('run','watch',$runId,'--repo',$Repo,'--exit-status','--interval','15') 'watch retried cloud jobs'
        if ($watch.code -eq 0) {
            Mark 'Cloud workflow passed after bounded retry'
            return $true
        }
    }
    Failure 'CLOUD_BUILD_FAILED' ('Workflow failed; details at ' + $script:CloudURL)
    return $false
}
function PublishStatus {
    # Repository Issues are disabled (GitHub API 410).
    # Save local report only; cloud workflows expose their own authenticated logs.
    Note 'REPORT CHANNEL: GitHub Issues disabled; retained full local JSON and cloud workflow URL'
}
function WriteReport {
    $data = [ordered]@{
        project='WRCX212 DM-32UV Kraken Analog Parrot'
        status=$script:State
        stage=$script:Stage
        started=$Started.ToString('o')
        ended=(Get-Date).ToString('o')
        tools=$Tools
        passed=@($Passes.ToArray())
        actions=@($Actions.ToArray())
        failures=@($Failures.ToArray())
        log=$Log
        cloud_url=$CloudURL
        radio_flashed=$false
        hardware_tested=$false
        ready_for_flash=$false
    }
    $data | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $Report -Encoding UTF8
    Note ('REPORT ' + $Report)
    Note ('FINISHED ' + $script:State)
}
try {
    Note 'WRCX212 AUTONOMOUS ENGINEERING CONTROLLER - safe, bounded, non-flashing'
    $Stage = 'DISCOVERY'
    $Tools.git = ToolPath @('git.exe','git')
    $Tools.python = ToolPath @('py.exe','python.exe','python3.exe')
    $Tools.wsl = ToolPath @('wsl.exe')
    $Tools.docker = ToolPath @('docker.exe')
    $Tools.gh = ToolPath @('gh.exe')
    $Tools.ollama = ToolPath @('ollama.exe')
    $Tools.aider = ToolPath @('aider.exe')
    $Tools.pwsh = ToolPath @('pwsh.exe')
    $Tools.gcc = ToolPath @('gcc.exe')
    foreach ($key in @($Tools.Keys)) { Note ('DISCOVER ' + $key + ': ' + [string]$Tools[$key]) }
    if ($Tools.ollama) { Note 'Ollama discovered; not executing unverified LLM-generated commands' }
    if ($Tools.aider) { Note 'Aider discovered; preserving source unless a reviewed patch is available' }

    $Stage = 'VERIFY_SOURCE'
    $checkout = AcquireSource
    $Stage = 'BUILD'
    $passed = $false
    if (-not $NoLocalBuild -and (WslDockerReady $Tools.wsl)) {
        $passed = BuildLocal $Tools.wsl
    } else {
        RecordAction 'Bypassing unavailable local Docker; using authenticated cloud workflow when possible'
    }
    if ($passed) {
        $State = 'BUILD_QEMU_PASSED_HARDWARE_UNTESTED'
    } else {
        if ($NoCloud) {
            $State = 'BLOCKED_CLOUD_DISABLED'
        } else {
            $dispatched = CloudBuild $Tools.gh
            if ($dispatched) { $State = 'CLOUD_BUILD_QEMU_PASSED_HARDWARE_UNTESTED' }
            else { $State = 'BLOCKED_LOCAL_AND_CLOUD' }
        }
    }
} catch {
    Failure 'FATAL' $_.Exception.Message
    $State = 'BLOCKED_SAFE'
} finally {
    WriteReport
    PublishStatus
}
