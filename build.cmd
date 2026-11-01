@echo off
setlocal
call "%~dp0find-unity.cmd"
if errorlevel 1 exit /b 1
if not exist "%~dp0Logs" mkdir "%~dp0Logs"
set "METHOD=SurvivorLab.Editor.BuildProject.Build"
if "%~1"=="--development" set "METHOD=SurvivorLab.Editor.BuildProject.BuildDevelopment"
if "%~1"=="--verify" set "METHOD=SurvivorLab.Editor.BuildProject.Verify"
start "" /b /wait "%EDITOR%" -batchmode -nographics -quit -projectPath "%~dp0." -executeMethod %METHOD% -logFile "%~dp0Logs\build.log"
set "BUILD_EXIT=%ERRORLEVEL%"
if not "%BUILD_EXIT%"=="0" (
  echo Build failed. See Logs\build.log for the actual error.
  exit /b %BUILD_EXIT%
)
if "%~1"=="--verify" exit /b 0
if not exist "%~dp0Builds\Windows\SurvivorLab.exe" exit /b 1
echo Build ready: Builds\Windows\SurvivorLab.exe
