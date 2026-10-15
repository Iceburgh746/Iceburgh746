@echo off
title WRCX212 CinemaHQ Windows Launcher
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0START_CINEMAHQ.ps1"
if errorlevel 1 pause
