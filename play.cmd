@echo off
if not exist "%~dp0Builds\Windows\SurvivorLab.exe" (
  echo No game build found. Run build.cmd first.
  exit /b 1
)
start "" "%~dp0Builds\Windows\SurvivorLab.exe"
