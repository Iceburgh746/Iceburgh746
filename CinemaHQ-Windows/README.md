# WRCX212 CinemaHQ Windows Launcher
Windows 10/11 x64 setup helper for the **original Android Cinema HQ**. This is not a native Windows build, and BlueStacks 5 is not portable.

## Use
1. Download both `START_CINEMAHQ.cmd` and `START_CINEMAHQ.ps1` from this directory into the SAME Windows folder.
2. Double-click `START_CINEMAHQ.cmd`.
3. If BlueStacks 5 is missing, install it from the official site opened by the launcher, then run the CMD again.
4. The script downloads the latest APK asset published in the developer's GitHub releases, launches BlueStacks and shows where the APK was saved.
5. In BlueStacks, select **Install APK** and choose the downloaded CinemaHQ.apk.

The APK is downloaded to `%LOCALAPPDATA%\WRCX212\CinemaHQ\CinemaHQ.apk`. To request the latest release again, remove that APK and rerun the CMD.

**Sources:** https://github.com/Inside4ndroid/Cinema-HQ/releases and https://www.bluestacks.com/

**Privacy:** The Cinema HQ project discloses a bandwidth-sharing SDK; review its opt-out options. The script does not bypass that feature.

**Limitation:** Scripts are published for convenience but have not been executed on your Windows PC. An emulator install is required. No APK is bundled or redistributed here.
