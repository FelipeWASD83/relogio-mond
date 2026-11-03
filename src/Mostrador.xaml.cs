using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace RelogioMond;

public partial class Mostrador : UserControl
{
    static readonly Geometry IconePlay = Geometry.Parse("M 0,0 L 9,5.5 L 0,11 Z");
    static readonly Geometry IconePausa = Geometry.Parse("M 0,0 H 3 V 11 H 0 Z M 6,0 H 9 V 11 H 6 Z");

    public event Action? PediuAnterior, PediuTocar, PediuProxima;
    public event Action<TimeSpan>? PediuBuscar;

    public bool TemFonteAnurati { get; private set; }

    bool _midiaVisivel;
    bool _controlesVisiveis;
    bool _pausado, _pausaVisivel;
    double _opacidadeAlvo;
    object? _capaAtual;
    TimeSpan _duracao;
    bool _podeBuscar, _arrastando, _sobreTrilho;
    double _fracaoArraste;
    int _pulouDir = 1;
    long _pulouEm;
    int _geracaoTroca;
    FrameworkElement? _fantasmaCapa, _fantasmaTexto;

    public Mostrador()
    {
        InitializeComponent();
        CarregarFontes();
        IconeTocar.Data = IconePlay;
        BotaoAnterior.MouseLeftButtonUp += (_, e) => { e.Handled = true; _pulouDir = -1; _pulouEm = Environment.TickCount64; PediuAnterior?.Invoke(); };
        BotaoTocar.MouseLeftButtonUp += (_, e) => { e.Handled = true; PediuTocar?.Invoke(); };
        BotaoProxima.MouseLeftButtonUp += (_, e) => { e.Handled = true; _pulouDir = 1; _pulouEm = Environment.TickCount64; PediuProxima?.Invoke(); };
        LinhaTempo.MouseLeftButtonDown += AoApertarTrilho;
        LinhaTempo.MouseMove += AoMoverTrilho;
        LinhaTempo.MouseLeftButtonUp += AoSoltarTrilho;
        LinhaTempo.LostMouseCapture += (_, _) => { if (_arrastando) { _arrastando = false; AtualizarTrilho(); } };
        LinhaTempo.MouseEnter += (_, _) => { _sobreTrilho = true; AtualizarTrilho(); };
        LinhaTempo.MouseLeave += (_, _) => { _sobreTrilho = false; AtualizarTrilho(); };
        foreach (var b in new[] { BotaoAnterior, BotaoTocar, BotaoProxima })
        {
            b.MouseEnter += (s, _) => ((Border)s!).Opacity = 0.7;
            b.MouseLeave += (s, _) => ((Border)s!).Opacity = 1;
        }
    }

    void CarregarFontes()
    {
        var pastaApp = new Uri(Caminhos.FontesDoApp.TrimEnd('\\') + "\\");
        var quicksand = Directory.Exists(Caminhos.FontesDoApp)
            ? new FontFamily(pastaApp, "./#Quicksand")
            : new FontFamily("Segoe UI");

        FontFamily dia;
        var anurati = Path.Combine(Caminhos.Fontes, "Anurati-Regular.otf");
        if (File.Exists(anurati))
        {
            dia = new FontFamily(new Uri(Caminhos.Fontes.TrimEnd('\\') + "\\"), "./#Anurati");
            TemFonteAnurati = true;
        }
        else
        {
            dia = quicksand;
            Dia.Peso = FontWeights.Light;
            Dia.Espacamento = 0.5;
        }

        Dia.Fonte = dia;
        Data.Fonte = quicksand; Data.Peso = FontWeights.Medium;
        Hora.Fonte = quicksand; Hora.Peso = FontWeights.Medium;
        TextoClima.Fonte = quicksand; TextoClima.Peso = FontWeights.Medium;
        foreach (var t in new[] { Titulo, Artista, TempoAtual, TempoTotal }) t.FontFamily = quicksand;
        Titulo.FontWeight = FontWeights.Bold;
        Artista.FontWeight = FontWeights.Medium;
        TempoAtual.FontWeight = TempoTotal.FontWeight = FontWeights.Medium;
    }

    public void AplicarEscala(double escala)
    {
        Escala.ScaleX = Escala.ScaleY = escala;
    }

    // reservarClima: com o clima ligado, a linha guarda o lugar dela enquanto a internet não responde,
    // senão o bloco cresceria depois de posicionado e o relógio ficaria mais baixo do que o configurado.
    public void AtualizarRelogio(DateTime agora, bool h24, string clima, bool reservarClima = false)
    {
        Dia.Texto = Idioma.Dia(agora);
        Data.Texto = Idioma.Data(agora);
        Hora.Texto = "- " + Idioma.Hora(agora, h24) + " -";
        TextoClima.Texto = clima;
        TextoClima.Visibility = !string.IsNullOrEmpty(clima) ? Visibility.Visible
            : reservarClima ? Visibility.Hidden : Visibility.Collapsed;
    }

    // O bloco da música inteiro (capa, título, tempo e barras) tem de ser mais estreito que o relógio
    // (a linha mais larga entre dia, data, hora e clima) em qualquer dia e idioma.
    double LarguraDoRelogio() => Math.Max(Math.Max(Dia.ActualWidth, Data.ActualWidth), Math.Max(Hora.ActualWidth, TextoClima.ActualWidth));

    public string Medidas() => $"musica={BlocoMidia.ActualWidth:0}x{BlocoMidia.ActualHeight:0} relogio={LarguraDoRelogio():0}";
    public bool MidiaMenorQueRelogio => BlocoMidia.ActualWidth < LarguraDoRelogio();

    public void AtualizarMidia(EstadoMidia m)
    {
        _midiaVisivel = m.Tem;
        // o bloco some quando não tem música; pausada, só a capa muda (AtualizarPausa)
        double alvo = m.Tem ? 1 : 0;
        if (Math.Abs(alvo - _opacidadeAlvo) > 0.001)
        {
            _opacidadeAlvo = alvo;
            BlocoMidia.BeginAnimation(OpacityProperty,
                new DoubleAnimation(alvo, TimeSpan.FromMilliseconds(alvo == 0 ? 700 : 450)) { EasingFunction = new SineEase() });
        }
        if (!m.Tem) return;

        bool haviaFaixa = Titulo.Text.Length > 0 || Artista.Text.Length > 0;
        bool faixaNova = haviaFaixa && (m.Titulo.Length > 0 || m.Artista.Length > 0)
                         && (Titulo.Text != m.Titulo || Artista.Text != m.Artista);
        if (faixaNova && _opacidadeAlvo > 0.5) IniciarTroca(Titulo.Text, Artista.Text, CapaPincel.ImageSource);
        if (Titulo.Text != m.Titulo) Titulo.Text = m.Titulo;
        if (Artista.Text != m.Artista) Artista.Text = m.Artista;
        Artista.Visibility = string.IsNullOrWhiteSpace(m.Artista) ? Visibility.Collapsed : Visibility.Visible;

        if (!ReferenceEquals(_capaAtual, m.Capa))
        {
            bool chegouDepois = _capaAtual == null && m.Capa != null && haviaFaixa;
            _capaAtual = m.Capa;
            CapaPincel.ImageSource = m.Capa;
            CapaImagem.Visibility = m.Capa != null ? Visibility.Visible : Visibility.Hidden;
            // a capa costuma chegar um pouco depois do título: entra com um fade em vez de pular
            if (chegouDepois && _opacidadeAlvo > 0.5)
                CapaImagem.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = new SineEase() });
        }

        IconeTocar.Data = m.Tocando ? IconePausa : IconePlay;
        _pausado = !m.Tocando;
        AtualizarPausa();

        if (m.Duracao > TimeSpan.Zero)
        {
            LinhaTempo.Visibility = Visibility.Visible;
            _duracao = m.Duracao;
            TempoTotal.Text = Tempo(m.Duracao);
            if (m.PodeBuscar != _podeBuscar)
            {
                _podeBuscar = m.PodeBuscar;
                if (!_podeBuscar) _arrastando = false;
                // player que não aceita pulo (transmissão ao vivo, alguns apps): a barra fica mais apagada
                Progresso.Opacity = _podeBuscar ? 1 : 0.6;
                AtualizarTrilho();
            }
            // arrastando, a barra e o tempo mostram o destino, não a posição de verdade
            if (!_arrastando)
            {
                TempoAtual.Text = Tempo(m.Posicao);
                MoverProgresso(Math.Clamp(m.Posicao.TotalSeconds / m.Duracao.TotalSeconds, 0, 1));
            }
        }
        else
        {
            LinhaTempo.Visibility = Visibility.Collapsed;
            _arrastando = false;
        }
    }

    void MoverProgresso(double fracao)
    {
        var largura = Trilho.ActualWidth;
        Progresso.Width = fracao * largura;
        Bola.Margin = new Thickness(fracao * largura - Bola.Width / 2, 0, 0, 0);
    }

    double FracaoDoMouse(MouseEventArgs e) =>
        Trilho.ActualWidth <= 0 ? 0 : Math.Clamp(e.GetPosition(Trilho).X / Trilho.ActualWidth, 0, 1);

    void MostrarArraste(double fracao)
    {
        _fracaoArraste = fracao;
        MoverProgresso(fracao);
        TempoAtual.Text = Tempo(TimeSpan.FromTicks((long)(_duracao.Ticks * fracao)));
    }

    void AoApertarTrilho(object s, MouseButtonEventArgs e)
    {
        if (!_podeBuscar || _duracao <= TimeSpan.Zero) return;
        e.Handled = true;
        _arrastando = true;
        LinhaTempo.CaptureMouse();
        MostrarArraste(FracaoDoMouse(e));
        AtualizarTrilho();
    }

    void AoMoverTrilho(object s, MouseEventArgs e)
    {
        if (_arrastando) MostrarArraste(FracaoDoMouse(e));
    }

    void AoSoltarTrilho(object s, MouseButtonEventArgs e)
    {
        if (!_arrastando) return;
        e.Handled = true;
        MostrarArraste(FracaoDoMouse(e));
        _arrastando = false;
        LinhaTempo.ReleaseMouseCapture();
        AtualizarTrilho();
        PediuBuscar?.Invoke(TimeSpan.FromTicks((long)(_duracao.Ticks * _fracaoArraste)));
    }

    // Como na ilha: com o mouse em cima ou arrastando, o trilho engrossa (3 -> 6) e aparece a bolinha.
    void AtualizarTrilho()
    {
        bool destaque = _podeBuscar && (_sobreTrilho || _arrastando);
        foreach (var b in new[] { FundoTrilho, Progresso })
            b.BeginAnimation(HeightProperty,
                new DoubleAnimation(destaque ? 6 : 3, TimeSpan.FromMilliseconds(140)) { EasingFunction = new SineEase() });
        Bola.Visibility = _arrastando ? Visibility.Visible : Visibility.Collapsed;
    }

    public bool Arrastando => _arrastando;

    // Retângulo da linha da minutagem (com folga em cima e embaixo, para acertar com o mouse).
    public Rect AreaDaBusca()
    {
        if (!_midiaVisivel || !_podeBuscar || LinhaTempo.Visibility != Visibility.Visible || LinhaTempo.ActualWidth <= 0) return Rect.Empty;
        var r = LinhaTempo.TransformToAncestor(this).TransformBounds(new Rect(0, 0, LinhaTempo.ActualWidth, LinhaTempo.ActualHeight));
        r.Inflate(0, 4);
        return r;
    }

    // ---- troca de faixa, no jeito da ilha (ContentSwap): o conteúdo velho sai deslizando e some, o novo entra
    // pelo outro lado um instante depois (12% da duração). "Próxima" empurra para a esquerda, "anterior" para a
    // direita (um botão apertado nos últimos 4 s decide; senão é a próxima). A capa também encolhe ao sair e
    // entra com um leve estouro (0,6 -> 1,08 -> 1).
    static readonly KeySpline CurvaSaida = new(0.4, 0, 1, 1);
    static readonly KeySpline CurvaEntrada = new(0.16, 1, 0.3, 1);

    static DoubleAnimationUsingKeyFrames Quadros(double ms, double atrasoMs, KeySpline curva, params (double fracao, double valor)[] pontos)
    {
        var a = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(ms),
            BeginTime = TimeSpan.FromMilliseconds(atrasoMs),
            FillBehavior = FillBehavior.HoldEnd,
        };
        foreach (var (f, v) in pontos)
            a.KeyFrames.Add(f <= 0 ? new LinearDoubleKeyFrame(v, KeyTime.FromPercent(0))
                                   : new SplineDoubleKeyFrame(v, KeyTime.FromPercent(f), curva));
        return a;
    }

    static TextBlock CopiaDoTexto(TextBlock modelo, string texto) => new()
    {
        Text = texto, FontSize = modelo.FontSize, FontFamily = modelo.FontFamily, FontWeight = modelo.FontWeight,
        Foreground = modelo.Foreground, Margin = modelo.Margin, TextTrimming = modelo.TextTrimming,
    };

    void LimparTroca()
    {
        _geracaoTroca++;
        if (_fantasmaCapa != null) Capa.Children.Remove(_fantasmaCapa);
        if (_fantasmaTexto != null) CelulaTextos.Children.Remove(_fantasmaTexto);
        _fantasmaCapa = _fantasmaTexto = null;
        CapaArte.BeginAnimation(OpacityProperty, null);
        Textos.BeginAnimation(OpacityProperty, null);
        CapaEscalaTroca.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        CapaEscalaTroca.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        CapaDeslocTroca.BeginAnimation(TranslateTransform.XProperty, null);
        TextosDeslocTroca.BeginAnimation(TranslateTransform.XProperty, null);
        CapaArte.Opacity = Textos.Opacity = 1;
        CapaEscalaTroca.ScaleX = CapaEscalaTroca.ScaleY = 1;
        CapaDeslocTroca.X = TextosDeslocTroca.X = 0;
    }

    void IniciarTroca(string tituloAntigo, string artistaAntigo, ImageSource? capaAntiga)
    {
        LimparTroca();
        int geracao = _geracaoTroca;
        int dir = Environment.TickCount64 - _pulouEm < 4000 ? _pulouDir : 1;

        // o que está saindo: cópias do que estava na tela, por cima do conteúdo ao vivo
        var fCapaEscala = new ScaleTransform();
        var fCapaDesloc = new TranslateTransform();
        var capa = new Grid
        {
            IsHitTestVisible = false,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new TransformGroup { Children = { fCapaEscala, fCapaDesloc } },
        };
        capa.Children.Add(new Border { CornerRadius = new CornerRadius(6), Background = CapaFundo.Background });
        if (capaAntiga != null)
            capa.Children.Add(new Border { CornerRadius = new CornerRadius(6), Background = new ImageBrush(capaAntiga) { Stretch = Stretch.UniformToFill } });
        Capa.Children.Insert(Capa.Children.IndexOf(CapaArte) + 1, capa);
        _fantasmaCapa = capa;

        var fTextoDesloc = new TranslateTransform();
        var texto = new StackPanel { IsHitTestVisible = false, RenderTransform = fTextoDesloc };
        texto.Children.Add(CopiaDoTexto(Titulo, tituloAntigo));
        if (!string.IsNullOrWhiteSpace(artistaAntigo)) texto.Children.Add(CopiaDoTexto(Artista, artistaAntigo));
        CelulaTextos.Children.Add(texto);
        _fantasmaTexto = texto;

        void Sair(double ms, double distancia, TranslateTransform desloc, UIElement elemento, ScaleTransform? escala)
        {
            double dur = ms * 0.7;
            desloc.BeginAnimation(TranslateTransform.XProperty, Quadros(dur, 0, CurvaSaida, (0, 0), (1, -dir * distancia)));
            elemento.BeginAnimation(OpacityProperty, Quadros(dur, 0, CurvaSaida, (0, 1), (1, 0)));
            if (escala != null)
            {
                var e = Quadros(dur, 0, CurvaSaida, (0, 1), (1, 0.6));
                escala.BeginAnimation(ScaleTransform.ScaleXProperty, e);
                escala.BeginAnimation(ScaleTransform.ScaleYProperty, e);
            }
        }

        DoubleAnimationUsingKeyFrames? maisLonga = null;
        void Entrar(double ms, double distancia, TranslateTransform desloc, UIElement elemento, ScaleTransform? escala)
        {
            double atraso = ms * 0.12;
            // valores de partida: durante o atraso a animação ainda não vale
            desloc.X = dir * distancia;
            elemento.Opacity = 0;
            var x = Quadros(ms, atraso, CurvaEntrada, (0, dir * distancia), (1, 0));
            desloc.BeginAnimation(TranslateTransform.XProperty, x);
            elemento.BeginAnimation(OpacityProperty, Quadros(ms, atraso, CurvaEntrada, (0, 0), (1, 1)));
            if (escala != null)
            {
                escala.ScaleX = escala.ScaleY = 0.6;
                var e = Quadros(ms, atraso, CurvaEntrada, (0, 0.6), (0.7, 1.08), (1, 1));
                escala.BeginAnimation(ScaleTransform.ScaleXProperty, e);
                escala.BeginAnimation(ScaleTransform.ScaleYProperty, e);
            }
            if (maisLonga == null || ms > maisLonga.Duration.TimeSpan.TotalMilliseconds) maisLonga = x;
        }

        Sair(280, 6, fCapaDesloc, capa, fCapaEscala);
        Entrar(280, 6, CapaDeslocTroca, CapaArte, CapaEscalaTroca);
        Sair(320, 22, fTextoDesloc, texto, null);
        Entrar(320, 22, TextosDeslocTroca, Textos, null);

        // o texto é o mais demorado: quando ele acaba, tira as cópias e devolve tudo ao estado normal
        maisLonga!.Completed += (_, _) =>
        {
            if (geracao == _geracaoTroca) LimparTroca();
        };
    }

    static string Tempo(TimeSpan t) => t.TotalHours >= 1
        ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
        : $"{(int)t.TotalMinutes}:{t.Seconds:00}";

    public bool MidiaVisivel => _midiaVisivel;

    // Pausada e sem o mouse na capa: escurece e mostra o pause. Com o mouse em cima, o pause
    // sai e entram os botões (que já trazem o play), para um não ficar por cima do outro.
    void AtualizarPausa()
    {
        bool mostrar = _pausado && !_controlesVisiveis;
        if (mostrar == _pausaVisivel) return;
        _pausaVisivel = mostrar;
        PausaCapa.BeginAnimation(OpacityProperty,
            new DoubleAnimation(mostrar ? 1 : 0, TimeSpan.FromMilliseconds(mostrar ? 350 : 180)) { EasingFunction = new SineEase() });
        IEasingFunction suave = mostrar
            ? new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 }
            : new SineEase();
        var escala = new DoubleAnimation(mostrar ? 1 : 0.75, TimeSpan.FromMilliseconds(mostrar ? 420 : 180)) { EasingFunction = suave };
        EscalaPausa.BeginAnimation(ScaleTransform.ScaleXProperty, escala);
        EscalaPausa.BeginAnimation(ScaleTransform.ScaleYProperty, escala);
    }

    // Para a captura em PNG: põe tudo no estado final, sem esperar animação.
    public void PularAnimacoes(bool controles = false)
    {
        LimparTroca();
        BlocoMidia.BeginAnimation(OpacityProperty, null);
        BlocoMidia.Opacity = _opacidadeAlvo;
        Controles.BeginAnimation(OpacityProperty, null);
        Controles.Opacity = controles ? 1 : 0;
        _controlesVisiveis = controles;
        _pausaVisivel = _pausado && !controles;
        PausaCapa.BeginAnimation(OpacityProperty, null);
        PausaCapa.Opacity = _pausaVisivel ? 1 : 0;
        EscalaPausa.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        EscalaPausa.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        EscalaPausa.ScaleX = EscalaPausa.ScaleY = _pausaVisivel ? 1 : 0.75;
    }

    // Retângulo da capa em coordenadas deste controle (para saber se o mouse está em cima).
    public Rect AreaDaCapa()
    {
        if (!_midiaVisivel || Capa.ActualWidth <= 0) return Rect.Empty;
        return Capa.TransformToAncestor(this).TransformBounds(new Rect(0, 0, Capa.ActualWidth, Capa.ActualHeight));
    }

    public void MostrarControles(bool mostrar)
    {
        if (mostrar == _controlesVisiveis) return;
        _controlesVisiveis = mostrar;
        Controles.BeginAnimation(OpacityProperty, new DoubleAnimation(mostrar ? 1 : 0, TimeSpan.FromMilliseconds(160)));
        AtualizarPausa();
    }
}
