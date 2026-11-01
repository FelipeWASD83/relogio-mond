# CLAUDE.md: relogio-mond

Relógio de área de trabalho no estilo da skin Mond do Rainmeter (pedido do Felipe em 01/11/2026):
dia da semana em Anurati, data, hora, clima e a música tocando (capa, título, artista, minutagem,
visualizador de barras). C# WPF no .NET 10, um processo só. **Repositório público** (aprovado por
ele em 01/11/2026), licença MIT: nada pessoal no código.

Versão atual: `versao.json` (anda junto com o topo do `CHANGELOG.md`).

## Como roda

```
atalho / Run do Windows ─► pythonw iniciar.pyw ─► dotnet app\RelogioMond.dll   (CREATE_NO_WINDOW)
```

- **Sem `.exe` próprio, de propósito** (`UseAppHost=false`): binário novo sem assinatura morre no
  Smart App Control. O app é DLL rodada pelo `dotnet.exe`. Como o `dotnet.exe` é programa de
  console, quem chama é o `iniciar.pyw` pelo `pythonw`.
- O `iniciar.pyw` também: recompila (`dotnet publish src -o app`) quando algum `.cs`/`.xaml`/`.csproj`
  é mais novo que a DLL (só existe `src/` na cópia de desenvolvimento); baixa a Anurati se faltar,
  conferindo o SHA-256; aplica atualização e desfaz se a versão nova fechar com erro antes de
  gravar o sinal de vida `dados/atualizacao/iniciou` (o app grava quando a janela abre). Não é
  por tempo: matar o relógio logo depois de abrir não desfaz nada.
- Instância única pelo mutex `Local\RelogioMond`.
- DPI: o `dotnet.exe` não declara DPI no manifesto, então o `Main` chama
  `SetProcessDpiAwarenessContext(PER_MONITOR_V2)` antes de tudo.

## Mapa

| Arquivo | O que faz |
|---|---|
| `src/Programa.cs` | `Main`, o `App` (bandeja, config, vigia do `config.json`) e os modos `--captura`, `--medir`, `--demo`, `--diagnostico`, `--testes` |
| `src/JanelaRelogio.cs` | A janela: estilos (toolwindow, noactivate, transparent, layered), dona = `Progman`, sempre `HWND_BOTTOM`, posição, laço das barras, mouse na capa, pausa em tela cheia |
| `src/Mostrador.xaml(.cs)` | O layout (dia, data, hora, clima, capa, título, tempo, barras) |
| `src/TextoEspacado.cs` | Texto com espaçamento entre letras e os acentos improvisados para a Anurati |
| `src/Visualizador.cs` | As barras (suavização de subida/descida e o pontinho de pico) |
| `src/CapturaAudio.cs` | Loopback WASAPI (NAudio) da saída padrão, FFT de 4096 e faixas logarítmicas de 40 Hz a 16 kHz |
| `src/Midia.cs` | Controle de mídia do Windows (GSMTC): sessão escolhida, título, capa, minutagem interpolada, botões |
| `src/Clima.cs` | Open-Meteo, a cada 15 min |
| `src/Idioma.cs` | Textos pt/en, dias, meses, códigos de tempo do WMO |
| `src/Atualizador.cs`, `JanelaAtualizacao.cs` | Procura e baixa a release, confere o SHA-256 (`digest` do GitHub) e deixa pronto para o `iniciar.pyw` |
| `src/Bandeja.cs`, `InicioAutomatico.cs` | Menu da bandeja e a linha em `HKCU\...\Run` |
| `src/Testes.cs`, `Demo.cs` | Testes sem som e a música de mentira da captura e da medição |
| `ferramentas/criar-atalho.ps1` | Atalho no Menu Iniciar (o projeto cria o próprio: o alvo é o `pythonw` do sistema) |
| `ferramentas/publicar-versao.ps1` | Monta o pacote (com o app compilado, sem `src/` nem a Anurati) e cria a release. **Publicar é decisão do Felipe** |

`dados/` (fora do git): `config.json`, `relogio.log`, `iniciar.log`, `medicao.txt`, `atualizacao/`.

## Decisões que não se desfazem sem motivo

- **A Anurati não vai para o git nem para a release.** É grátis só para uso pessoal. Cada PC baixa
  a sua (URL e SHA-256 no `iniciar.pyw`). Sem ela, o dia cai na Quicksand Light com mais espaçamento.
- **A Anurati só tem A-Z.** O `TextoEspacado` desenha a letra base e improvisa o sinal: cedilha é um
  traço embaixo do C (pedido dele), agudo um traço inclinado em cima, e assim por diante.
- **Espaçamento pelo desenho da letra** (1.0.1, ele achou o da 1.0.0 largo e desigual): o vão entre
  letras é medido entre os contornos (`BuildGeometry().Bounds`), não pela largura de avanço da fonte.
  Vírgula e ponto grudam na letra de antes. Valores atuais: dia 0.3, data/hora/clima 0.2 (em fração
  do tamanho). Barras com 42 de altura.
- **Só a capa recebe clique.** A janela é `WS_EX_TRANSPARENT`; um timer de 50 ms tira o bit enquanto
  o mouse está em cima da capa (aí aparecem os controles) e põe de volta ao sair.
- **Visível no Win+D** porque a janela é "owned" pela `Progman`. Conferido no build 26300 em 01/11/2026.
- **As barras dormem.** Sem música, ou com as barras paradas no silêncio por 1,5 s, o app para de
  assinar o `CompositionTarget.Rendering`; volta quando o nível do som passa de 0,0005. Com jogo em
  tela cheia (`SHQueryUserNotificationState`) ou janela em primeiro plano cobrindo o relógio, a
  captura desliga.
- **O processo não consegue ler a própria sessão de mídia** (o pedido de propriedades trava). Por isso
  o teste toca o WAV mudo num processo filho (`--tocar-mudo`).
- **Kaspersky** avisa o loopback como ".NET Host está usando o microfone". É a captura das barras;
  quem permite é ele.

## Rodar, testar, conferir

- Compilar: `dotnet publish src -c Release -o app` (ou só abrir pelo `iniciar.pyw`).
- Testes (sem som; abrem um player mudo em outro processo): `dotnet app\RelogioMond.dll --testes`.
- Foto sem janela: `dotnet app\RelogioMond.dll --captura saida.png [--idioma en] [--data "2026-11-03 18:42"] [--controles] [--sem-midia]`.
- fps: `dotnet app\RelogioMond.dll --demo --medir 10` grava `dados\medicao.txt` (mediana, mínimo,
  CPU, memória e onde a janela ficou). Em 01/11/2026: **mediana 83-87 fps, mínimo 73** (1.0.0) e
  **mediana 90, mínimo 84** (1.0.1), CPU ~2%, ~215 MB, em 3440x1440 a 125%.
- O que está tocando agora: `dotnet app\RelogioMond.dll --diagnostico`.

## Armadilhas

- `.ps1` com acento precisa de UTF-8 com BOM (PowerShell 5.1).
- Rodar o app pelo `dotnet` na mão funciona, mas sem `RELOGIO_LANCADOR` o botão de atualizar recusa
  (quem troca os arquivos é o `iniciar.pyw`).
- Na cópia de desenvolvimento (tem `.git`) o atualizador recusa: atualiza pelo git.
