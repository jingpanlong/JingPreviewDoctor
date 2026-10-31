$ErrorActionPreference = 'Stop'
$sourceRoot = $PSScriptRoot
$buildOutput = Join-Path $sourceRoot 'dist'
New-Item -ItemType Directory -Force -Path $buildOutput | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Need .NET Framework 4.0 or later C# compiler.' }
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ "/out:$buildOutput\JingPreviewDoctor.exe" "/win32manifest:$sourceRoot\app.manifest" "/win32icon:$sourceRoot\app.ico" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.dll "$sourceRoot\PreviewDoctor.cs" "$sourceRoot\Productivity.cs" "$sourceRoot\Convenience.cs" "$sourceRoot\LayoutQuality.cs" "$sourceRoot\SystemChecks.cs" "$sourceRoot\AssemblyInfo.cs"
if ($LASTEXITCODE -ne 0) { throw 'Main program compilation failed.' }
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ "/out:$buildOutput\JingPreviewDoctor-Setup.exe" "/win32manifest:$sourceRoot\app.manifest" "/win32icon:$sourceRoot\app.ico" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/resource:$buildOutput\JingPreviewDoctor.exe,App" "/resource:$sourceRoot\使用说明.txt,Readme" "$sourceRoot\Setup.cs" "$sourceRoot\AssemblyInfo.cs"
if ($LASTEXITCODE -ne 0) { throw 'Setup compilation failed.' }
Copy-Item -LiteralPath "$sourceRoot\使用说明.txt" -Destination $buildOutput -Force
Write-Output "Build complete: $buildOutput"
