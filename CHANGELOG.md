# Changelog

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
