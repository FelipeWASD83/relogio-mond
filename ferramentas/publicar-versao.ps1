# Publica a versão atual do Relógio Mond como release no GitHub (é ela que o "Procurar atualização" baixa).
# Antes: subir a versão em versao.json e CHANGELOG.md, commitar e dar push.
# Precisa do gh logado (gh auth login) com permissão no repositório. Publicar é decisão do Felipe.
$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
Set-Location $raiz

function Rodar([string]$exe, [string[]]$argumentos) {
    # No PowerShell 5.1, com 'Stop', qualquer linha no stderr vira erro fatal mesmo dando certo.
    $ErrorActionPreference = 'Continue'
    $saida = & $exe @argumentos 2>&1 | ForEach-Object { "$_" }
    return [pscustomobject]@{ codigo = $LASTEXITCODE; saida = (($saida -join "`n").Trim()) }
}

$info = Get-Content (Join-Path $raiz 'versao.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$versao = $info.versao
$repo = $info.repositorio
$changelog = Get-Content (Join-Path $raiz 'CHANGELOG.md') -Raw -Encoding UTF8
if ($changelog -notmatch "## $([regex]::Escape($versao)) ") { throw "O CHANGELOG.md não tem a seção da versão $versao." }

if ((Rodar git @('status', '--porcelain')).saida) { throw 'Há mudanças não commitadas. Commite antes de publicar.' }
$r = Rodar git @('fetch', '-q', 'origin')
if ($r.codigo -ne 0) { throw "Não deu para falar com o GitHub (git fetch): $($r.saida)" }
$local = (Rodar git @('rev-parse', 'HEAD')).saida
$r = Rodar git @('rev-parse', '@{u}')
if ($r.codigo -ne 0) { throw 'Este ramo não está ligado ao GitHub. Rode: git push -u origin main' }
if ($local -ne $r.saida) { throw 'O commit atual ainda não foi enviado (git push) ou está atrás do GitHub.' }
if ((Rodar gh @('release', 'view', "v$versao", '--repo', $repo)).codigo -eq 0) { throw "A release v$versao já existe." }

# notas: só a seção desta versão do CHANGELOG
$linhas = $changelog -split "`r?`n"
$achou = $linhas | Where-Object { $_ -match "^## $([regex]::Escape($versao)) " } | Select-Object -First 1
$inicio = [array]::IndexOf($linhas, $achou)
$notas = @()
for ($i = $inicio + 1; $i -lt $linhas.Length; $i++) { if ($linhas[$i] -match '^## ') { break }; $notas += $linhas[$i] }

$tmp = Join-Path $env:TEMP "relogio-mond-release-$versao"
if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
$pasta = Join-Path $tmp 'relogio-mond'
New-Item -ItemType Directory -Force $pasta | Out-Null
$arqNotas = Join-Path $tmp 'notas.md'
[IO.File]::WriteAllText($arqNotas, (($notas -join "`n").Trim()), [Text.UTF8Encoding]::new($false))

# o pacote leva o app já compilado (o PC de quem atualiza não precisa do SDK) e nada de dados/ nem da Anurati
$r = Rodar git @('archive', '--format=tar', '-o', (Join-Path $tmp 'fonte.tar'), 'HEAD')
if ($r.codigo -ne 0) { throw "Não deu para exportar o commit: $($r.saida)" }
$r = Rodar tar @('-xf', (Join-Path $tmp 'fonte.tar'), '-C', $pasta)
if ($r.codigo -ne 0) { throw "Não deu para abrir o commit exportado: $($r.saida)" }
$r = Rodar dotnet @('publish', (Join-Path $pasta 'src'), '-c', 'Release', '-o', (Join-Path $pasta 'app'), '--nologo', '-v', 'q')
if ($r.codigo -ne 0) { throw "A compilação falhou: $($r.saida)" }
foreach ($fora in @('src', 'fontes', '.gitignore', 'CLAUDE.md')) {
    $p = Join-Path $pasta $fora
    if (Test-Path $p) { Remove-Item $p -Recurse -Force }
}
Get-ChildItem (Join-Path $pasta 'app') -Filter *.pdb | Remove-Item -Force
$zip = Join-Path $tmp "relogio-mond-$versao.zip"
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($pasta, $zip, [IO.Compression.CompressionLevel]::Optimal, $true)

Write-Host "Publicando v$versao em $repo com $zip"
$r = Rodar gh @('release', 'create', "v$versao", $zip, '--repo', $repo, '--title', "Relógio Mond $versao", '--notes-file', $arqNotas, '--target', $local)
if ($r.codigo -ne 0) { throw "O gh não criou a release: $($r.saida)" }
Write-Host $r.saida
Write-Host 'Pronto. O "Procurar atualização" das outras cópias já enxerga esta versão.'
