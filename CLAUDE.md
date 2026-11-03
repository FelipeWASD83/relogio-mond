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
| `src/Mostrador.xaml(.cs)` | O layout (dia, data, hora, clima, e o bloco da música: capa, título, tempo, barras) |
| `src/TextoEspacado.cs` | Texto com espaçamento entre letras e os acentos improvisados para a Anurati |
| `src/Visualizador.cs` | As barras (sobe em ~20 ms, desce em ~120 ms; sem pontinho de pico) |
| `src/CapturaAudio.cs` | Loopback WASAPI (NAudio) da saída padrão, FFT de 4096, faixas logarítmicas de 40 Hz a 16 kHz e ganho automático |
| `src/Midia.cs` | Controle de mídia do Windows (GSMTC): sessão escolhida, título, capa, minutagem interpolada, botões |
| `src/Clima.cs` | Open-Meteo, a cada 15 min |
| `src/Idioma.cs` | Textos pt/en, dias, meses, códigos de tempo do WMO |
| `src/Atualizador.cs`, `JanelaAtualizacao.cs` | Procura e baixa a release, confere o SHA-256 (`digest` do GitHub) e deixa pronto para o `iniciar.pyw` |
| `src/Bandeja.cs`, `InicioAutomatico.cs` | Menu da bandeja e a linha em `HKCU\...\Run` |
| `src/Testes.cs`, `Demo.cs` | Testes sem som e o áudio sintético (parecido com música) da captura e da medição, que passa pela mesma FFT e pelo mesmo ganho do áudio real |
| `ferramentas/criar-atalho.ps1` | Atalho no Menu Iniciar (o projeto cria o próprio: o alvo é o `pythonw` do sistema) |
| `ferramentas/publicar-versao.ps1` | Monta o pacote (com o app compilado, sem `src/` nem a Anurati) e cria a release. **Publicar é decisão do Felipe** |

`dados/` (fora do git): `config.json`, `relogio.log`, `iniciar.log`, `medicao.txt`, `atualizacao/`.

## Decisões que não se desfazem sem motivo

- **A Anurati não vai para o git nem para a release.** É grátis só para uso pessoal. Cada PC baixa
  a sua (URL e SHA-256 no `iniciar.pyw`). Sem ela, o dia cai na Quicksand Light com mais espaçamento.
- **A Anurati só tem A-Z.** O `TextoEspacado` desenha a letra base e improvisa o sinal: cedilha é um
  risco na diagonal embaixo do C, na inclinação do agudo (pedido dele na 1.0.2; na 1.0.0 era um traço
  reto), agudo um traço inclinado em cima, e assim por diante.
- **Espaçamento pelo desenho da letra** (1.0.1, ele achou o da 1.0.0 largo e desigual): o vão entre
  letras é medido entre os contornos (`BuildGeometry().Bounds`), não pela largura de avanço da fonte.
  Vírgula e ponto grudam na letra de antes. Valores atuais (1.0.2): dia 100 com vão 0.3; data e hora
  28 e clima 19, vão 0.2. Sem pontinho de pico.
- **O bloco da música inteiro é menor que o relógio** (1.1.2, com um desenho dele: retângulo pequeno
  centrado embaixo do grande; eu tinha encolhido só o visualizador e ele corrigiu: é tudo, capa,
  título e minutagem). Largura fixa de 336 (`BlocoMidia`), contra 423 do dia mais estreito (TERÇA);
  capa 80, título 18, artista 13, tempo 12, barras com 14 de altura. O `--captura` mede as duas
  larguras (`MidiaMenorQueRelogio`) e sai com erro se o bloco não for menor; conferido nos 7 dias em
  pt e em 3 em en. Dia mais estreito em outro idioma/fonte: se o relógio ficar abaixo de 336, o
  teste acusa e é preciso encolher o bloco.
- **Posição**: `altura` é a fração da tela onde fica o CENTRO da janela (padrão 0.425: 0.40 no começo,
  0.37 na 1.1.0, 0.44 na 1.1.1; na 1.1.2 o topo do dia sobe 10 px, y=352, e como o bloco da música
  encolheu a janela, o centro vai a 0.425). O espaço da linha do clima fica reservado
  (`Visibility.Hidden`) até a internet responder e a janela se reposiciona no `SizeChanged`; antes disso
  o relógio ficava ~20 px mais baixo que o configurado. Com clima desligado a linha some (`Collapsed`).
- **Barras fortes pelo ganho automático** (1.1.0, ele achou o visualizador fraco): a faixa mais forte
  dos últimos segundos (meia-vida ~4 s, piso 0,3 contra ruído) vira o topo, e uma curva de 1,3 dá
  contraste. Antes o nível dependia do volume que chegava no loopback. Sem teste com música real
  tocando (só sinal sintético pela mesma FFT); se ficar fraco ou saturado de verdade, os números
  estão em `CapturaAudio.Espectro` (`(db + 56) / 46`, `0.3`, `1.3`).
- **Pausa na capa** (1.1.0): `PausaCapa` escurece (preto 72%) e mostra o pause branco com um pulinho
  de escala; sai quando o mouse entra na capa (aí valem os botões, que já mostram o play). O bloco
  da música não esmaece mais ao pausar.
- **A captura de áudio abre uma vez e fica** (1.1.3, ele reportou "às vezes as barras param"). O NAudio avisa
  `RecordingStopped` pelo contexto de sincronia do WPF, depois da captura nova já aberta; o aviso da antiga
  apagava `_dispositivo` da nova e o `Garantir` a reabria a cada 2 s, para sempre (o teste de `--testes`
  mede: eram 5 aberturas em 7 s, agora 2). O handler só vale para a captura em uso. Também: amostra
  NaN/infinita é zerada (NaN no `_picoLembrado` matava o ganho automático até fechar o app), e o
  `ConferirBarras` (a cada 250 ms) reabre a captura se a música toca e nenhum som chega há mais de 4 s
  (espera dobra de 6 s até 2 min se for silêncio de verdade) e reinscreve o `CompositionTarget.Rendering`
  se o WPF ficou 2,5 s sem desenhar. Tudo isso anota no `relogio.log`: se as barras morrerem de novo, é lá
  que está a pista.
- **Pausa por tela cheia só vale no monitor do relógio** (1.1.3). `SHQueryUserNotificationState` (estados
  2 e 3) acende com tela cheia em qualquer monitor, e uma janela do Claude no monitor em pé já bastava
  para as barras morrerem com o relógio à vista. `Nativo.TelaCheiaOcupada(hwnd)` compara o monitor da
  janela em primeiro plano com o do relógio; o estado 4 (apresentação) continua pausando. Pausa e
  liberação vão para o `relogio.log` com o motivo.
- **A tela cheia tem que ocupar o monitor inteiro e não ser a área de trabalho** (1.2.0). Clicar no desktop
  deixa a `Progman` em primeiro plano e o `SHQueryUserNotificationState` segue em 2 (ocupado), o que pausava
  as barras (reproduzido: log "tela cheia no monitor do relógio"). `Nativo.TelaCheiaOcupada` agora ignora
  `EhAreaDeTrabalho(fg)` e exige a janela cobrindo o monitor (folga de 2 px).
- **Barras no movimento da ilha** (1.2.0, pedido dele): `Visualizador.Amostrar` roda a ~30 Hz (`Quadro` acumula o
  `dt`), sobe na hora e desce 0,09 por leitura (`Queda`), `Passo` percorre de uma leitura à outra em 70 ms em
  linha reta, a barra cresce do centro e o piso é 0,2. O ganho automático continua em `CapturaAudio`. Teste
  `barras com o movimento da ilha` no `--testes` (0,50 / 1,00 / 0,91).
- **Barras mais fortes e bonitas, todas brancas** (1.4.0, pedido dele; ele rejeitou a cor da capa nas barras, então
  nada de degradê colorido). `Visualizador`: altura 32 (`Mostrador.xaml`, com a margem de cima 12 e a de baixo do bloco
  10 para a janela não crescer), barra a 62% do passo, piso 0,14, cada leitura mistura 25% de cada vizinha e
  realça (`pow 0,88 * 1,12`), `DropShadowEffect` branco de brilho (blur 16, 50%). Curva do ganho em
  `CapturaAudio` de 1,3 para 1,1. Medido 170 fps de mediana com o brilho.
- **Minutagem clicável** (1.2.0). A linha da minutagem (`LinhaTempo`, mais 4 px em cima e embaixo) tira o
  `WS_EX_TRANSPARENT` como a capa (`VerMouse` no `JanelaRelogio`, que também segura enquanto arrasta).
  Arrastar mostra o destino (barra mais grossa, bolinha, tempo) e soltar chama `Midia.Buscar`
  (`TryChangePlaybackPositionAsync` com o `StartTime` somado). Depois do pulo, a posição do destino vale por até
  2 s ou até o player avisar uma posição perto dele (`_seguraAte`), senão a barra voltava. `PodeBuscar` vem de
  `Controls.IsPlaybackPositionEnabled`. Testado com o player mudo (`--testes`) e com o mouse de verdade
  (arrastou a 90% e a faixa foi ao fim). **Teste com o mouse pelo PowerShell: ele vê coordenadas
  virtualizadas (÷ 1,25 a 125%); passar pixel de tela direto erra o alvo.**
- **Troca de faixa no jeito da ilha** (1.3.0, `ContentSwap.cs` de lá): `Mostrador.IniciarTroca`. O que estava na tela
  vira cópia por cima (capa em `Capa`, texto em `CelulaTextos`), sai em 70% do tempo (curva 0,4/0/1/1) deslizando
  6 DIP (capa, encolhe a 0,6) e 22 DIP (texto) para o lado oposto ao da entrada; o novo entra em 280/320 ms
  com atraso de 12% (curva 0,16/1/0,3/1), a capa com 0,6 -> 1,08 -> 1. Direção: o último botão da capa apertado
  nos últimos 4 s (anterior = -1, próxima = +1), senão +1. Detecta pela mudança de título/artista com a faixa
  anterior já na tela; capa que chega depois ganha fade de 260 ms. Visto em quadros capturados do relógio real
  (player mudo duplo, `--tocar-mudo "Título"`); com duas sessões tocando ao mesmo tempo o relógio alterna entre elas.
- **O Smart App Control barra DLL compilada aqui, por hash** (1.3.0; 0x800711C7, o relógio fechava sem abrir e
  o `iniciar.log` dizia "fechou com o código 3762504530"). `compilar_se_preciso` (iniciar.pyw) compila em
  `build\publish` com `-p:SourceRevisionId=<letras sorteadas>`, roda `dotnet RelogioMond.dll --check-load`
  (sai com 0 logo no começo do `Main`; se o Windows barrar, nem chega lá) e só então copia para `app\`; barrou?
  sorteia outro carimbo (até 15). O `publicar-versao.ps1` faz igual. **Qualquer `dotnet publish` solto para
  testar também pode gerar DLL barrada**: usar o `app\` do iniciar.pyw para rodar teste.
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
- Foto sem janela: `dotnet app\RelogioMond.dll --captura saida.png [--idioma en] [--data "2026-11-03 18:42"] [--controles] [--sem-midia] [--pausado]`.
- fps: `dotnet app\RelogioMond.dll --demo --medir 10` grava `dados\medicao.txt` (mediana, mínimo,
  CPU, memória e onde a janela ficou). Em 01/11/2026: **mediana 83-87 fps, mínimo 73** (1.0.0),
  **90 / 84** (1.0.1), **92 / 88** (1.0.2), **82 / 80** (1.1.0, já sintetizando áudio e rodando a
  FFT a cada quadro, então mais perto do real) e **104 / 94** (1.1.2), CPU ~2%, ~205 MB, em 3440x1440
  a 125%. A janela da 1.1.2 mede 1550x520 e o topo fica em y=352.
- O que está tocando agora: `dotnet app\RelogioMond.dll --diagnostico`.

## Armadilhas

- `.ps1` com acento precisa de UTF-8 com BOM (PowerShell 5.1).
- Rodar o app pelo `dotnet` na mão funciona, mas sem `RELOGIO_LANCADOR` o botão de atualizar recusa
  (quem troca os arquivos é o `iniciar.pyw`).
- Na cópia de desenvolvimento (tem `.git`) o atualizador recusa: atualiza pelo git.
