@echo off
setlocal
call "%~dp0find-unity.cmd"
if errorlevel 1 exit /b 1
start "" "%EDITOR%" -projectPath "%~dp0."
