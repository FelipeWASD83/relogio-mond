# Changelog

## 1.5.1 (04/11/2026)

**Mudou**

- Ícone novo, da família do Desktop Glow: uma lua crescente com os ponteiros do relógio dentro (Mond é lua em alemão).

## 1.5.0 (03/11/2026)

Junta as versões 1.4.0, 1.4.1 e 1.4.2, que não chegaram a ser publicadas, e acrescenta o comando de fechar.

**Novo**

- `--quit` (ou `--sair`) fecha o relógio que está aberto, igual ao "Sair" da bandeja: `dotnet app\RelogioMond.dll --quit`. É o mesmo comando que a ilha (`--sair`) e o Fundo Vivo (`--quit`) já têm, e é o que o setup usa para fechar os apps antes de atualizar.

**Mudou**

- Visualizador de barras mais forte, todo branco e sem brilho, no visual da primeira versão: barras finas assentadas embaixo, com o pontinho de pico que fica um instante e cai devagar, e mais altas que antes.
- Barras suaves: cada barra recebe um pouco das vizinhas, a barra sobe em duas ou três leituras em vez de saltar de uma vez e os graves deixaram de ficar em degraus iguais.

## 1.4.2 (03/11/2026)

**Mudou**

- Visualizador mais suave: cada barra recebe um pouco das vizinhas, então o conjunto forma uma curva contínua em vez de barras desiguais lado a lado, e a barra sobe em duas ou três leituras em vez de saltar de uma vez.
- Graves mais certos: as barras da esquerda liam o mesmo pedaço do espectro e ficavam em degraus iguais; agora cada uma lê o seu ponto.

## 1.4.1 (03/11/2026)

**Mudou**

- O visualizador de barras volta ao visual da primeira versão: barras finas assentadas embaixo, cada uma com um pontinho de pico que fica um instante e cai devagar. O brilho saiu. O movimento continua o da ilha (sobe na hora, desce devagar), e as barras seguem mais altas que na 1.3 (altura 36).

## 1.4.0 (03/11/2026)

**Mudou**

- Visualizador de barras mais forte e mais bonito, todo branco: as barras ficaram mais altas (de 14 para 32 de altura) e mais grossas, a silhueta é suavizada entre as barras vizinhas (parece uma onda contínua, não palitos soltos), o conjunto é levemente realçado e tem um brilho branco suave por trás. O bloco da música não cresceu: o espaço entre o tempo e as barras diminuiu na mesma medida.
- O contraste do som ficou menos achatado (curva de 1,3 para 1,1), então as barras do meio também sobem mais.

## 1.3.0 (02/11/2026)

**Novo**

- Animação de troca de faixa, a mesma da ilha: a capa, o título e o artista antigos saem deslizando e sumindo (a capa também encolhe) enquanto os novos entram pelo lado oposto, a capa com um leve estouro. "Próxima" empurra para a esquerda e "anterior" para a direita (apertando os botões da capa); sem botão, é como se fosse a próxima. A capa que chega um instante depois do título entra com um fade.

**Corrigido**

- O Windows (Smart App Control) às vezes barrava a DLL recém-compilada e o relógio fechava sem abrir. Agora o `iniciar.pyw` compila com um carimbo sorteado, confere se o Windows deixa carregar a DLL (`--check-load`) e, se barrar, sorteia outro carimbo e compila de novo. O `publicar-versao.ps1` faz o mesmo no pacote da release.

## 1.2.0 (02/11/2026)

**Novo**

- Dá para mudar a minutagem: clique na barra da música ou arraste. Enquanto arrasta, a barra engrossa, aparece uma bolinha e o tempo mostra o destino; ao soltar, a faixa pula. Se o player não aceita pular (transmissão ao vivo, alguns apps), a barra fica mais apagada e não responde.

**Mudou**

- O visualizador usa as mesmas animações da ilha: a barra sobe na hora e desce devagar, cada leitura do som é percorrida de forma lisa em 70 ms, a altura cresce a partir do centro e o piso é de 20%. O espectro agora é lido 30 vezes por segundo (antes, a cada quadro), então o relógio ficou mais leve: a mediana subiu de 88 para 150 fps na medição.

**Corrigido**

- As barras travavam ao clicar na área de trabalho: o Windows continua dizendo "ocupado" com a área de trabalho em primeiro plano e o relógio entendia isso como tela cheia. Agora ele ignora a área de trabalho e só pausa se a janela em primeiro plano ocupa o monitor inteiro.

## 1.1.3 (01/11/2026)

**Corrigido**

- As barras paravam sempre que havia um app em tela cheia (ou maximizado cobrindo o monitor inteiro) em QUALQUER monitor, como um vídeo no monitor em pé, mesmo com o relógio à vista no principal. Agora só pausam se a tela cheia está no mesmo monitor do relógio. Cada pausa e liberação fica anotada no `relogio.log`, com o motivo.
- As barras às vezes paravam de funcionar. A captura do som se reabria sozinha a cada 2 segundos depois de qualquer troca (música pausando e voltando, saída de som mudando, tela cheia saindo) e, nessa bagunça, podia ficar sem receber som. Agora ela abre uma vez e fica.
- Um valor inválido vindo do driver de som (NaN) travava as barras até fechar o relógio. Agora é ignorado.
- Rede de segurança: se a música está tocando e nenhum som chega por mais de 4 segundos, o relógio reabre a captura sozinho (e anota no `relogio.log`). Se o Windows parar de pedir o desenho das barras (monitor que dormiu, tela bloqueada), ele pede de novo.
- Música bem baixa agora também acorda as barras depois de um silêncio.

## 1.1.2 (01/11/2026)

**Mudou**

- O bloco da música inteiro (capa, título, minutagem e barras) ficou menor e sempre mais estreito que o relógio: 336 de largura, contra 423 do dia mais estreito (TERÇA). A capa passou de 112 para 80, o título de 25 para 18, o artista de 17 para 13, a minutagem de 14 para 12, as barras ficaram mais baixas (altura 14) e os botões da capa menores. Título comprido termina em "...".
- O relógio sobe 10 px (`altura` padrão 0.425, que já considera a janela menor).

**Corrigido**

- A posição saía uns 20 px mais baixa do que o configurado: a linha do clima só aparecia depois que a internet respondia, com a janela já posicionada. Agora o espaço do clima fica reservado desde o início e a janela se reposiciona quando o tamanho muda.

## 1.1.1 (01/11/2026)

**Mudou**

- O relógio desce na tela: `altura` padrão de 0.37 para 0.44. Quem já tinha o `config.json` mantém o valor que estava lá.

## 1.1.0 (01/11/2026)

**Novo**

- Animação de pausa: com a música pausada, a capa escurece e aparece um ícone de pause branco por cima. Passando o mouse na capa, o pause sai e entram os botões.

**Mudou**

- Barras mais fortes: ganho automático (a faixa mais forte vai ao topo, então música baixa ou volume baixo enche as barras como a alta), subida mais rápida e mais contraste entre barra alta e baixa.
- Visualizador menor (altura 20) e mais afastado do bloco da música.
- O relógio sobe um pouco na tela (`altura` de 0.40 para 0.37). Quem já tinha o `config.json` mantém o valor que estava lá.
- A música pausada não esmaece mais o bloco inteiro; só a capa muda.

## 1.0.2 (01/11/2026)

**Mudou**

- Data, hora e clima maiores.
- Visualizador de barras ainda mais baixo, sem o pontinho de pico em cima das barras.
- O Ç do dia agora tem um risco na diagonal embaixo do C, na mesma inclinação do acento do Á.

## 1.0.1 (01/11/2026)

**Mudou**

- Espaçamento entre as letras mais apertado e igual em todas: agora é medido pelo desenho de cada letra, então letra estreita como o I não fica mais isolada.
- Visualizador de barras mais baixo e com barras mais finas.

## 1.0.0 (01/11/2026)

**Novo**

- Relógio de área de trabalho no estilo Mond: dia da semana com a fonte Anurati, data, hora e clima.
- Na Anurati, que não tem acento, o Ç ganha um traço embaixo do C e o Á um traço em cima do A.
- Música tocando pelo controle de mídia do Windows: capa, título, artista e minutagem com barra de progresso.
- Visualizador de barras que escuta o som da saída padrão e troca sozinho quando a saída muda.
- Botões de anterior, tocar/pausar e próxima ao passar o mouse na capa.
- Clima atual pelo Open-Meteo, atualizado a cada 15 minutos.
- Português e inglês, automático pelo idioma do Windows ou escolhido no menu.
- Fica no centro do monitor principal, um pouco acima do meio, atrás das janelas e visível no Win+D.
- As barras param com jogo ou vídeo em tela cheia e quando uma janela cobre o relógio.
- Menu no ícone da bandeja, iniciar com o Windows, `config.json` aplicado na hora.
- Procurar e instalar atualização pelas releases do GitHub, com o SHA-256 conferido e volta automática se a versão nova não abrir.
