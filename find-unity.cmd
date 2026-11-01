@echo off
set "EDITOR=%UNITY_EDITOR%"
if defined EDITOR if exist "%EDITOR%" exit /b 0
set "EDITOR=%~dp0Tools\Unity6000.0.74f1\Editor\Unity.exe"
if exist "%EDITOR%" exit /b 0
set "EDITOR=%ProgramFiles%\Unity\Hub\Editor\6000.0.74f1\Editor\Unity.exe"
if exist "%EDITOR%" exit /b 0
echo Unity 6000.0.74f1 is required. Install it through Unity Hub with Windows Build Support.
echo Or set UNITY_EDITOR to the full path of Unity.exe.
exit /b 1
