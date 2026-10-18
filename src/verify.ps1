param([switch]$UnitOnly)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
& (Join-Path $PSScriptRoot 'build.ps1')
$sources = Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | Where-Object { $_.Name -ne 'Program.cs' } | Select-Object -ExpandProperty FullName
$assets = Join-Path $PSScriptRoot 'assets'
$resources = @('app.ico','paused.ico','countdown.ico','calendar-2026.xml') | ForEach-Object { '/resource:' + (Join-Path $assets $_) + ',' + $_ }
$testExe = Join-Path $PSScriptRoot '.build\Tests.exe'
& $compiler /nologo /target:exe /codepage:65001 /utf8output "/out:$testExe" /reference:System.dll /reference:System.Core.dll /reference:System.Xml.dll /reference:System.Xml.Linq.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:Microsoft.CSharp.dll $resources $sources
if ($LASTEXITCODE -ne 0) { throw '测试程序编译失败' }
if ($UnitOnly) { & $testExe --unit-only }
else { & $testExe (Join-Path $PSScriptRoot '每天定时关机.exe') (Split-Path -Parent $PSScriptRoot) }
if ($LASTEXITCODE -ne 0) { throw '验证未通过' }
