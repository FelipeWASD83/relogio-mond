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

    public bool TemFonteAnurati { get; private set; }

    bool _midiaVisivel;
    bool _controlesVisiveis;
    bool _pausado, _pausaVisivel;
    double _opacidadeAlvo;
    object? _capaAtual;

    public Mostrador()
    {
        InitializeComponent();
        CarregarFontes();
        IconeTocar.Data = IconePlay;
        BotaoAnterior.MouseLeftButtonUp += (_, e) => { e.Handled = true; PediuAnterior?.Invoke(); };
        BotaoTocar.MouseLeftButtonUp += (_, e) => { e.Handled = true; PediuTocar?.Invoke(); };
        BotaoProxima.MouseLeftButtonUp += (_, e) => { e.Handled = true; PediuProxima?.Invoke(); };
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

    public void AtualizarRelogio(DateTime agora, bool h24, string clima)
    {
        Dia.Texto = Idioma.Dia(agora);
        Data.Texto = Idioma.Data(agora);
        Hora.Texto = "- " + Idioma.Hora(agora, h24) + " -";
        TextoClima.Texto = clima;
        TextoClima.Visibility = string.IsNullOrEmpty(clima) ? Visibility.Collapsed : Visibility.Visible;
    }

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
            TempoAtual.Text = Tempo(m.Posicao);
            TempoTotal.Text = Tempo(m.Duracao);
            var trilho = ((FrameworkElement)Progresso.Parent).ActualWidth;
            Progresso.Width = Math.Clamp(m.Posicao.TotalSeconds / m.Duracao.TotalSeconds, 0, 1) * trilho;
        }
        else
        {
            LinhaTempo.Visibility = Visibility.Collapsed;
        }
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
