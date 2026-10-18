$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
$assetDir = Join-Path $PSScriptRoot 'assets'
$buildDir = Join-Path $PSScriptRoot '.build'
New-Item -ItemType Directory -Path $buildDir -Force | Out-Null
$iconTool = Join-Path $buildDir 'BuildIcons.exe'
& $compiler /nologo /target:exe "/out:$iconTool" /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'tools\BuildIcons.cs')
if ($LASTEXITCODE -ne 0) { throw '图标工具编译失败' }
& $iconTool $assetDir
if ($LASTEXITCODE -ne 0) { throw '图标构建失败' }
$sources = Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | Where-Object { $_.Name -notlike 'Tests*' } | Select-Object -ExpandProperty FullName
$target = Join-Path $PSScriptRoot '每天定时关机.exe'
$resources = @('app.ico', 'paused.ico', 'countdown.ico', 'calendar-2026.xml') | ForEach-Object { '/resource:' + (Join-Path $assetDir $_) + ',' + $_ }
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /utf8output "/out:$target" "/win32icon:$(Join-Path $assetDir 'app.ico')" /reference:System.dll /reference:System.Core.dll /reference:System.Xml.dll /reference:System.Xml.Linq.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:Microsoft.CSharp.dll $resources $sources
if ($LASTEXITCODE -ne 0) { throw '编译失败' }
Write-Output $target
