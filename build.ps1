param([switch]$SingleFile)
$ErrorActionPreference = 'Stop'
$taskSource = Join-Path $PSScriptRoot 'src'
if (!(Test-Path -LiteralPath $taskSource)) { throw 'Source directory missing' }
$taskRuntime = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
if (!(Test-Path -LiteralPath (Join-Path $taskRuntime 'mscorlib.dll'))) { throw '.NET Framework 4.x runtime required' }
$taskDotnet = (Get-Command dotnet -ErrorAction Stop).Source
$taskSdkRoot = Join-Path (Split-Path $taskDotnet) 'sdk'
if (!(Test-Path -LiteralPath $taskSdkRoot)) { throw '.NET SDK required' }
$taskCompiler = Get-ChildItem -LiteralPath $taskSdkRoot -Filter csc.dll -Recurse | Where-Object { $_.FullName -like '*Roslyn*bincore*' } | Sort-Object FullName -Descending | Select-Object -First 1
if (!$taskCompiler) { throw 'Roslyn C# compiler missing' }
$taskOutput = Join-Path $PSScriptRoot 'bin'
if (!(Test-Path -LiteralPath $taskOutput)) { New-Item -ItemType Directory -Path $taskOutput | Out-Null }
$taskArguments = @('/noconfig', '/nostdlib+', '/target:winexe', '/platform:anycpu', '/optimize+', ('/out:' + (Join-Path $taskOutput 'MouseTester-DPI.exe')))
foreach ($taskName in @('mscorlib', 'System', 'System.Core', 'System.Drawing', 'System.Windows.Forms')) {
    $taskReference = Join-Path $taskRuntime ($taskName + '.dll')
    if (!(Test-Path -LiteralPath $taskReference)) { throw "Missing reference: $taskReference" }
    $taskArguments += '/reference:' + $taskReference
}
foreach ($taskName in @('Program.cs', 'StartupLog.cs', 'RawInputSource.cs', 'RawInputSource.Interop.cs', 'CalibrationForm.cs', 'Measurement.cs', 'MotionView.cs', 'MouseDevices.cs', 'AssemblyInfo.cs')) {
    $taskFile = Join-Path $taskSource $taskName
    if (!(Test-Path -LiteralPath $taskFile)) { throw "Source file missing: $taskName" }
    $taskArguments += $taskFile
}
& $taskDotnet $taskCompiler.FullName @taskArguments
if ($LASTEXITCODE -ne 0) { throw 'Compile failed' }
Write-Output "Built: $taskOutput/MouseTester-DPI.exe"
