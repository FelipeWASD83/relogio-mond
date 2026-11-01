"""Abre o Relógio Mond sem janela de console.

O app é uma DLL do .NET rodada pelo dotnet.exe (que é assinado): um RelogioMond.exe
compilado aqui seria binário novo e o Smart App Control barraria. O dotnet.exe é
programa de console, então quem chama é este script, pelo pythonw, com CREATE_NO_WINDOW.

Também é o supervisor da atualização (o esquema do Clave):
- o app baixa e confere a versão nova e fecha com o código 3;
- aqui a versão atual vai para dados/atualizacao/anterior, a nova entra no lugar e o
  app reabre; se ela fechar com erro antes de abrir a janela (o app grava
  dados/atualizacao/iniciou quando a janela aparece), a anterior volta sozinha.
dados/ (configuração) e fontes/ (a Anurati) nunca são tocados.

Na cópia de desenvolvimento (com src/), recompila quando o código é mais novo que a DLL.
"""
from __future__ import annotations

import hashlib
import json
import os
import shutil
import subprocess
import sys
import time
import urllib.request
from pathlib import Path

RAIZ = Path(__file__).resolve().parent
DADOS = RAIZ / "dados"
ATUALIZACAO = DADOS / "atualizacao"
DLL = RAIZ / "app" / "RelogioMond.dll"
GERENCIADOS = ["app", "ferramentas", "icone", "iniciar.pyw", "versao.json", "CHANGELOG.md", "README.md", "LICENSE"]

# A Anurati é grátis só para uso pessoal e não pode ir junto no repositório público:
# cada PC baixa a sua, conferida pelo SHA-256.
ANURATI = RAIZ / "fontes" / "Anurati-Regular.otf"
ANURATI_URL = "https://media.githubusercontent.com/media/linnovs/taijipan/HEAD/assets/fonts/Anurati/Anurati-Regular.otf"
ANURATI_SHA256 = "9b6c41cb089ea9f682f177d2a5dc21e1828c39e6f20b9a4c11d999de912e00c3"

CODIGO_APLICAR = 3
CREATE_NO_WINDOW = 0x08000000


def log(texto: str) -> None:
    try:
        DADOS.mkdir(parents=True, exist_ok=True)
        with open(DADOS / "iniciar.log", "a", encoding="utf-8") as f:
            f.write(f"{time.strftime('%Y-%m-%d %H:%M:%S')} {texto}\n")
    except OSError:
        pass


def dotnet() -> str:
    achado = shutil.which("dotnet")
    if achado:
        return achado
    padrao = Path(os.environ.get("ProgramFiles", r"C:\Program Files")) / "dotnet" / "dotnet.exe"
    return str(padrao)


def compilar_se_preciso() -> None:
    src = RAIZ / "src"
    if not src.is_dir():
        return
    fontes = [p for p in src.rglob("*") if p.suffix in {".cs", ".xaml", ".csproj"} and "obj" not in p.parts and "bin" not in p.parts]
    if DLL.exists() and fontes and max(p.stat().st_mtime for p in fontes) <= DLL.stat().st_mtime:
        return
    log("compilando (código mais novo que a DLL)")
    r = subprocess.run([dotnet(), "publish", str(src), "-c", "Release", "-o", str(RAIZ / "app"), "--nologo", "-v", "q"],
                       capture_output=True, text=True, creationflags=CREATE_NO_WINDOW)
    if r.returncode != 0:
        log("compilação falhou:\n" + (r.stdout or "") + (r.stderr or ""))


def baixar_anurati() -> None:
    if ANURATI.exists():
        return
    try:
        req = urllib.request.Request(ANURATI_URL, headers={"User-Agent": "RelogioMond"})
        with urllib.request.urlopen(req, timeout=30) as r:
            dados = r.read()
        if hashlib.sha256(dados).hexdigest() != ANURATI_SHA256:
            log("a Anurati baixada não confere com o SHA-256; ficou a fonte de reserva")
            return
        ANURATI.parent.mkdir(parents=True, exist_ok=True)
        ANURATI.write_bytes(dados)
        log("Anurati baixada")
    except OSError as e:
        log(f"sem a Anurati ({e}); ficou a fonte de reserva")


def _ler(arq: Path) -> dict | None:
    try:
        return json.loads(arq.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return None


def _escrever(arq: Path, dados: dict) -> None:
    arq.parent.mkdir(parents=True, exist_ok=True)
    arq.write_text(json.dumps(dados, ensure_ascii=False), encoding="utf-8")


def _copiar(origem: Path, destino: Path) -> None:
    if origem.is_dir():
        shutil.copytree(origem, destino)
    else:
        destino.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(origem, destino)


def _restaurar(origem: Path, alvo: Path) -> None:
    if alvo.is_dir():
        shutil.rmtree(alvo, ignore_errors=True)
    elif alvo.exists():
        alvo.unlink()
    if not alvo.exists():
        shutil.move(str(origem), str(alvo))
        return
    # o Windows não deixou apagar (antivírus, Explorer aberto na pasta): devolve item por item
    if origem.is_dir() and alvo.is_dir():
        for item in list(origem.iterdir()):
            _restaurar(item, alvo / item.name)
        shutil.rmtree(origem, ignore_errors=True)


def aplicar_pendente() -> str | None:
    pronta = _ler(ATUALIZACAO / "pronta.json")
    if not pronta:
        return None
    (ATUALIZACAO / "pronta.json").unlink(missing_ok=True)
    novo = Path(pronta["pasta"])
    if not (novo / "app" / "RelogioMond.dll").is_file():
        log(f"pacote pronto incompleto em {novo}; ignorado")
        return None
    anterior = ATUALIZACAO / "anterior"
    shutil.rmtree(anterior, ignore_errors=True)
    anterior.mkdir(parents=True)
    try:
        for nome in GERENCIADOS:
            if (RAIZ / nome).exists():
                shutil.move(str(RAIZ / nome), str(anterior / nome))
        for nome in GERENCIADOS:
            if (novo / nome).exists():
                _copiar(novo / nome, RAIZ / nome)
    except Exception as e:  # qualquer falha no meio da troca: tudo de volta
        log(f"troca falhou ({e}); voltando")
        reverter()
        return None
    shutil.rmtree(ATUALIZACAO / "novo", ignore_errors=True)
    versao = pronta.get("versao", "")
    _escrever(ATUALIZACAO / "aplicada.json", {"versao": versao})
    log(f"atualizado para {versao}")
    return versao


def reverter() -> None:
    anterior = ATUALIZACAO / "anterior"
    if not anterior.is_dir():
        return
    for nome in GERENCIADOS:
        if (anterior / nome).exists():
            _restaurar(anterior / nome, RAIZ / nome)


def rodar() -> int:
    env = dict(os.environ, RELOGIO_LANCADOR="1", RELOGIO_PYTHONW=sys.executable)
    p = subprocess.Popen([dotnet(), str(DLL)], cwd=str(RAIZ), env=env, creationflags=CREATE_NO_WINDOW)
    return p.wait()


def main() -> None:
    os.chdir(RAIZ)
    compilar_se_preciso()
    baixar_anurati()
    recem_atualizado: str | None = None
    iniciou = ATUALIZACAO / "iniciou"
    while True:
        versao = aplicar_pendente()
        if versao:
            recem_atualizado = versao
        iniciou.unlink(missing_ok=True)
        codigo = rodar()
        if codigo == CODIGO_APLICAR:
            continue
        if recem_atualizado and codigo != 0 and not iniciou.exists():
            log(f"a versão {recem_atualizado} fechou com {codigo} logo ao abrir; voltando para a anterior")
            reverter()
            (ATUALIZACAO / "aplicada.json").unlink(missing_ok=True)
            _escrever(ATUALIZACAO / "voltou.json", {"versao": recem_atualizado})
            recem_atualizado = None
            continue
        if codigo != 0:
            log(f"o relógio fechou com o código {codigo}")
        break


if __name__ == "__main__":
    main()
