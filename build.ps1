$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $projectRoot 'SafeScreen.exe'

if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'The 64-bit .NET Framework C# compiler was not found.'
}

& $compiler `
    /nologo `
    /target:winexe `
    /optimize+ `
    /platform:anycpu `
    /reference:System.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    "/win32manifest:$projectRoot\app.manifest" `
    "/win32icon:$projectRoot\SafeScreen.ico" `
    "/resource:$projectRoot\SafeScreen.ico,Awake.SafeScreen.SafeScreen.ico" `
    "/resource:$projectRoot\SafeScreenLogo.png,Awake.SafeScreen.SafeScreenLogo.png" `
    "/out:$output" `
    "$projectRoot\SafeScreen.cs"

if ($LASTEXITCODE -ne 0) {
    throw "SafeScreen compilation failed with exit code $LASTEXITCODE."
}

Write-Host "Built $output"
