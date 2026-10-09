$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1') -Console
$repoDir = $PSScriptRoot
& (Join-Path $repoDir 'dist\GPD-WIN3-Keymapper.Console.exe') --self-test
if ($LASTEXITCODE -ne 0) { throw 'Self-test failed.' }
