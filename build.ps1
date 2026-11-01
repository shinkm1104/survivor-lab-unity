param([string]$UnityPath, [switch]$VerifyOnly)
$ErrorActionPreference = 'Stop'
$projectPath = $PSScriptRoot
if (-not $UnityPath) { $UnityPath = $env:UNITY_EDITOR }
if (-not $UnityPath) { $UnityPath = Join-Path $projectPath 'Tools\Unity6000.0.74f1\Editor\Unity.exe' }
if (-not (Test-Path -LiteralPath $UnityPath)) { $UnityPath = Join-Path $env:ProgramFiles 'Unity\Hub\Editor\6000.0.74f1\Editor\Unity.exe' }
if (-not (Test-Path -LiteralPath $UnityPath)) { throw "Unity Editor missing: $UnityPath" }
$method = if ($VerifyOnly) { 'SurvivorLab.Editor.BuildProject.Verify' } else { 'SurvivorLab.Editor.BuildProject.Build' }
$logFile = Join-Path $projectPath 'Logs\build.log'
New-Item -ItemType Directory -Force -Path (Split-Path $logFile) | Out-Null
$buildProcess = Start-Process -FilePath $UnityPath -ArgumentList @('-batchmode','-nographics','-quit','-projectPath',('"' + $projectPath + '"'),'-executeMethod',$method,'-logFile',('"' + $logFile + '"')) -WindowStyle Hidden -Wait -PassThru
if ($buildProcess.ExitCode -ne 0) { throw "Unity failed ($($buildProcess.ExitCode)). See $logFile" }
Get-Content -LiteralPath $logFile -Tail 25
