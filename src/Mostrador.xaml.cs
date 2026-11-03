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

    public Mostrador()
    {
        InitializeComponent();
        CarregarFontes();
        IconeTocar.Data = IconePlay;
        BotaoAnterior.MouseLeftButtonUp += (_, e) => { e.Handled = true; PediuAnterior?.Invoke(); };
        BotaoTocar.MouseLeftButtonUp += (_, e) => { e.Handled = true; PediuTocar?.Invoke(); };
        BotaoProxima.MouseLeftButtonUp += (_, e) => { e.Handled = true; PediuProxima?.Invoke(); };
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

        if (Titulo.Text != m.Titulo) Titulo.Text = m.Titulo;
        if (Artista.Text != m.Artista) Artista.Text = m.Artista;
        Artista.Visibility = string.IsNullOrWhiteSpace(m.Artista) ? Visibility.Collapsed : Visibility.Visible;

        if (!ReferenceEquals(_capaAtual, m.Capa))
        {
            _capaAtual = m.Capa;
            CapaPincel.ImageSource = m.Capa;
            CapaImagem.Visibility = m.Capa != null ? Visibility.Visible : Visibility.Hidden;
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
