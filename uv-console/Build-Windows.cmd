@echo off
setlocal EnableExtensions
cd /d "%~dp0"

echo ============================================================
echo   UV Console by WRCX 212
echo   Native Windows x64 build
echo ============================================================
echo.

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if not exist "%CSC%" (
  echo ERROR: The Windows .NET Framework C# compiler was not found.
  echo This build targets the .NET Framework already present on most Windows 10 systems.
  echo Install the Microsoft .NET Framework 4.8 Developer Pack, then run this file again.
  pause
  exit /b 1
)

if not exist "dist" mkdir "dist"
if exist "dist\UVConsole.exe" del /q "dist\UVConsole.exe"
if exist "dist\UVConsole.exe.config" del /q "dist\UVConsole.exe.config"

"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /checked- /out:"dist\UVConsole.exe" /win32icon:"assets\UVConsole.ico" /win32manifest:"app.manifest" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /resource:"assets\ConsoleFaceplate.png",UVConsole.ConsoleFaceplate.png src\Program.cs src\Protocol.cs src\RadioSession.cs src\Controls.cs src\MainForm.cs

if errorlevel 1 (
  echo.
  echo BUILD FAILED. The compiler messages above identify the source line.
  pause
  exit /b 1
)

copy /y "UVConsole.exe.config" "dist\UVConsole.exe.config" >nul
copy /y "LICENSE" "dist\LICENSE" >nul
copy /y "NOTICE" "dist\NOTICE" >nul

echo.
echo BUILD COMPLETE:
echo   %CD%\dist\UVConsole.exe
echo.
echo The EXE is portable. Keep its .config, LICENSE, and NOTICE beside it.
echo No browser or web server is used.
echo.
pause
