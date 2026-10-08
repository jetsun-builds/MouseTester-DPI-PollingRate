param([switch]$RunTests, [switch]$SingleFile)
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$taskSource = Join-Path $taskRoot 'MouseTester/MouseTester'
if (!(Test-Path -LiteralPath $taskSource)) { throw 'Source directory missing' }
$taskRuntime = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
if (!(Test-Path -LiteralPath (Join-Path $taskRuntime 'mscorlib.dll'))) { throw '.NET Framework 4.x runtime required' }
$taskDotnet = (Get-Command dotnet -ErrorAction Stop).Source
$taskSdkRoot = Join-Path (Split-Path $taskDotnet) 'sdk'
if (!(Test-Path -LiteralPath $taskSdkRoot)) { throw '.NET SDK required' }
$taskCompiler = Get-ChildItem -LiteralPath $taskSdkRoot -Filter csc.dll -Recurse | Where-Object { $_.FullName -like '*Roslyn*bincore*' } | Sort-Object FullName -Descending | Select-Object -First 1
if (!$taskCompiler) { throw 'Roslyn C# compiler missing' }
$taskOutput = Join-Path $taskRoot 'portable'
if ($SingleFile) { $taskOutput = Join-Path $taskRoot 'single-file' }
if (!(Test-Path -LiteralPath $taskOutput)) { New-Item -ItemType Directory -Path $taskOutput | Out-Null }
$taskArguments = @('/noconfig', '/nostdlib+', '/target:winexe', '/platform:anycpu', '/optimize+', ('/out:' + (Join-Path $taskOutput 'MouseTester-CPI.exe')))
foreach ($taskName in @('mscorlib', 'System', 'System.Core', 'System.Drawing', 'System.Windows.Forms', 'System.Data', 'System.Xml')) {
    $taskReference = Join-Path $taskRuntime ($taskName + '.dll')
    if (!(Test-Path -LiteralPath $taskReference)) { throw "Missing reference: $taskReference" }
    $taskArguments += '/reference:' + $taskReference
}

foreach ($taskName in @('Program.cs', 'StartupLog.cs', 'RawInputSource.cs', 'RawInputSource.Interop.cs', 'CalibrationForm.cs', 'Measurement.cs', 'MotionView.cs', 'MouseDevices.cs')) {
    $taskFile = Join-Path $taskSource $taskName
    if (!(Test-Path -LiteralPath $taskFile)) { throw 'Source file missing' }
    $taskArguments += $taskFile
}
& $taskDotnet $taskCompiler.FullName @taskArguments
if ($LASTEXITCODE -ne 0) { throw 'Compile failed' }
$taskConfig = Join-Path $taskSource 'app.config'
if (!(Test-Path -LiteralPath $taskConfig)) { throw 'app.config missing' }
if (!$SingleFile) { Copy-Item -LiteralPath $taskConfig -Destination (Join-Path $taskOutput 'MouseTester-CPI.exe.config') }
$taskLicense = Join-Path $taskRoot 'LICENSE'
if (Test-Path -LiteralPath $taskLicense) { Copy-Item -LiteralPath $taskLicense -Destination $taskOutput }
Write-Output "Built: $taskOutput/MouseTester-CPI.exe"
if ($RunTests) {
    $taskTests = Join-Path $taskRoot 'tests/MeasurementTests.cs'
    if (!(Test-Path -LiteralPath $taskTests)) { throw 'Tests missing' }
    $taskTestArguments = @('/noconfig', '/nostdlib+', '/target:exe', ('/out:' + (Join-Path $taskOutput 'MeasurementTests.exe')), ('/reference:' + (Join-Path $taskOutput 'MouseTester-CPI.exe')))
    foreach ($taskName in @('mscorlib', 'System', 'System.Core', 'System.Drawing', 'System.Windows.Forms')) { $taskTestArguments += '/reference:' + (Join-Path $taskRuntime ($taskName + '.dll')) }
    $taskTestArguments += $taskTests
    & $taskDotnet $taskCompiler.FullName @taskTestArguments
    if ($LASTEXITCODE -ne 0) { throw 'Test compile failed' }
    & (Join-Path $taskOutput 'MeasurementTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
    & (Join-Path $taskOutput 'MeasurementTests.exe') --startup
    if ($LASTEXITCODE -ne 0) { throw 'Startup test failed' }
}
