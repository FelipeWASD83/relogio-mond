# Cria (ou refaz) o atalho "Relógio Mond" no Menu Iniciar e, se faltar, compila o app e baixa a fonte.
# Uso: powershell -ExecutionPolicy Bypass -File ferramentas\criar-atalho.ps1 [-Remover] [-Abrir]
# O atalho abre o iniciar.pyw pelo pythonw (sem console). O "Iniciar com o Windows" o próprio relógio liga.
param([switch]$Remover, [switch]$Abrir)
$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$lnkArq = Join-Path ([Environment]::GetFolderPath('Programs')) 'Relógio Mond.lnk'

if ($Remover) {
    if (Test-Path $lnkArq) { Remove-Item $lnkArq -Force; Write-Host "Tirado: $lnkArq" }
    exit 0
}

$pythonw = Get-Command pythonw.exe -All -ErrorAction SilentlyContinue |
    Where-Object { $_.Source -notmatch 'WindowsApps' } | Select-Object -First 1 -ExpandProperty Source
if (-not $pythonw) { throw 'Não achei o pythonw.exe (o Python do python.org, não o atalho da Microsoft Store).' }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Falta o .NET 10 Desktop Runtime (winget install Microsoft.DotNet.DesktopRuntime.10).' }

$shell = New-Object -ComObject WScript.Shell
$lnk = $shell.CreateShortcut($lnkArq)
$lnk.TargetPath = $pythonw
$lnk.Arguments = '"' + (Join-Path $raiz 'iniciar.pyw') + '"'
$lnk.WorkingDirectory = $raiz
$lnk.IconLocation = (Join-Path $raiz 'icone\icone.ico') + ',0'
$lnk.Description = 'Relógio, data, clima e música tocando na área de trabalho, no estilo Mond'
$lnk.Save()
Write-Host "Atalho: $lnkArq"

if ($Abrir) { Start-Process $pythonw -ArgumentList ('"' + (Join-Path $raiz 'iniciar.pyw') + '"') -WorkingDirectory $raiz }
