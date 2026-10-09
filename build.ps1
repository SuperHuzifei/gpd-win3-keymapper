param([switch]$Console)
$ErrorActionPreference = 'Stop'
$repoDir = $PSScriptRoot
$distDir = Join-Path $repoDir 'dist'
New-Item -ItemType Directory -Path $distDir -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework x64 compiler was not found.' }
$target = if ($Console) { 'exe' } else { 'winexe' }
$name = if ($Console) { 'GPD-WIN3-Keymapper.Console.exe' } else { 'GPD-WIN3-Keymapper.exe' }
& $compiler /nologo ("/target:" + $target) /platform:x64 ("/out:" + (Join-Path $distDir $name)) /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.Linq.dll /reference:System.Management.dll (Join-Path $repoDir 'GpdHid.cs') (Join-Path $repoDir 'GpdMapper.cs')
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output (Join-Path $distDir $name)
