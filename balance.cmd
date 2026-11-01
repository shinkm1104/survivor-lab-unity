@echo off
rem balance.cmd                                       : measure class balance (CSV in docs\reports\data)
rem balance.cmd --replay CLASS SEED [MOVER] [CHOOSER] : replay one run twice and print its timeline
rem   CLASS 0-3, MOVER q / kite / still, CHOOSER learned / rule / random
setlocal
call "%~dp0find-unity.cmd"
if errorlevel 1 exit /b 1
if not exist "%~dp0Logs" mkdir "%~dp0Logs"
set "MOVER=%~4"
set "CHOOSER=%~5"
if "%MOVER%"=="" set "MOVER=q"
if "%CHOOSER%"=="" set "CHOOSER=learned"
if "%~1"=="--replay" goto replay
start "" /b /wait "%EDITOR%" -batchmode -nographics -quit -projectPath "%~dp0." -executeMethod SurvivorLab.Editor.BalanceLab.Measure -logFile "%~dp0Logs\balance.log"
if errorlevel 1 goto failed
echo Done: docs\reports\data\summary.csv
exit /b 0
:replay
start "" /b /wait "%EDITOR%" -batchmode -nographics -quit -projectPath "%~dp0." -executeMethod SurvivorLab.Editor.BalanceLab.Replay -class %~2 -seed %~3 -mover %MOVER% -chooser %CHOOSER% -every 2 -out "%~dp0Logs\replay.txt" -logFile "%~dp0Logs\balance.log"
if errorlevel 1 goto failed
type "%~dp0Logs\replay.txt"
exit /b 0
:failed
echo Failed. See Logs\balance.log
exit /b 1
