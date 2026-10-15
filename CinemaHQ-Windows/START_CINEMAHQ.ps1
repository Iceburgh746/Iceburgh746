$ErrorActionPreference = 'Stop'
$root = Join-Path $env:LOCALAPPDATA 'WRCX212\CinemaHQ'
New-Item -ItemType Directory -Path $root -Force | Out-Null
Write-Host 'WRCX212 CinemaHQ Windows Launcher' -ForegroundColor Cyan
$player = @("$env:ProgramFiles\BlueStacks_nxt\HD-Player.exe", "${env:ProgramFiles(x86)}\BlueStacks_nxt\HD-Player.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $player) {
  Write-Host 'BlueStacks 5 is needed to run the Android Cinema HQ app on Windows.'
  Start-Process 'https://www.bluestacks.com/'
  Read-Host 'After installing BlueStacks 5, rerun START_CINEMAHQ.cmd. Press Enter to exit'
  exit
}
$apk = Join-Path $root 'CinemaHQ.apk'
if (-not (Test-Path $apk)) {
  Write-Host 'Finding APK from official developer GitHub releases...'
  try {
    $release = Invoke-RestMethod -Uri 'https://api.github.com/repos/Inside4ndroid/Cinema-HQ/releases/latest' -Headers @{ 'User-Agent'='WRCX212-CinemaHQ-Launcher' }
    $asset = $release.assets | Where-Object { $_.name -match '\.apk$' } | Select-Object -First 1
    if (-not $asset) { throw 'No APK asset found in latest release.' }
    Write-Host "Downloading $($asset.name)..."
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $apk -UseBasicParsing
    if ((Get-Item $apk).Length -lt 1000000) { throw 'Downloaded file is unexpectedly small.' }
    $stream = [System.IO.File]::OpenRead($apk)
    try { $header = New-Object byte[] 2; [void]$stream.Read($header,0,2) }
    finally { $stream.Close() }
    if ($header[0] -ne 0x50 -or $header[1] -ne 0x4b) { throw 'Downloaded file is not an APK ZIP container.' }
  } catch {
    Remove-Item $apk -ErrorAction SilentlyContinue
    Write-Warning $_.Exception.Message
    Start-Process 'https://github.com/Inside4ndroid/Cinema-HQ/releases/latest'
    Read-Host 'Download an official APK manually. Press Enter to exit'
    exit 1
  }
}
Write-Host 'Opening BlueStacks and the downloaded APK folder...'
Start-Process -FilePath $player
Start-Process -FilePath 'explorer.exe' -ArgumentList ('/select,"' + $apk + '"')
Write-Host 'In BlueStacks choose Install APK on its sidebar and select CinemaHQ.apk.'
Write-Warning 'The Cinema HQ project discloses a bandwidth-sharing SDK. Review privacy settings and opt-out choices before use.'
Read-Host 'Press Enter to close this window'
