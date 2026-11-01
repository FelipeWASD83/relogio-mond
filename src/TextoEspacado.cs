using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace RelogioMond;

// Texto com espaçamento entre letras (o WPF não tem letter-spacing). Quando a fonte
// não tem uma letra acentuada (a Anurati só tem A-Z), desenha a letra base e
// improvisa o sinal com traços: cedilha vira um risco diagonal embaixo do C, agudo um traço
// inclinado em cima, e assim por diante.
public sealed class TextoEspacado : FrameworkElement
{
    public static readonly DependencyProperty TextoProperty = Reg(nameof(Texto), "");
    public static readonly DependencyProperty FonteProperty = Reg(nameof(Fonte), new FontFamily("Segoe UI"));
    public static readonly DependencyProperty TamanhoProperty = Reg(nameof(Tamanho), 20.0);
    public static readonly DependencyProperty PesoProperty = Reg(nameof(Peso), FontWeights.Normal);
    public static readonly DependencyProperty EspacamentoProperty = Reg(nameof(Espacamento), 0.2);
    public static readonly DependencyProperty CorProperty = Reg(nameof(Cor), (Brush)Brushes.White);
    // Caixa justa na altura das maiúsculas, com folga em cima e embaixo para os sinais improvisados.
    public static readonly DependencyProperty SoMaiusculasProperty = Reg(nameof(SoMaiusculas), false);

    static DependencyProperty Reg(string nome, object padrao) =>
        DependencyProperty.Register(nome, padrao.GetType().IsSubclassOf(typeof(Brush)) ? typeof(Brush) : padrao.GetType(),
            typeof(TextoEspacado),
            new FrameworkPropertyMetadata(padrao, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                (d, _) => ((TextoEspacado)d)._pecas = null));

    public string Texto { get => (string)GetValue(TextoProperty); set => SetValue(TextoProperty, value); }
    public FontFamily Fonte { get => (FontFamily)GetValue(FonteProperty); set => SetValue(FonteProperty, value); }
    public double Tamanho { get => (double)GetValue(TamanhoProperty); set => SetValue(TamanhoProperty, value); }
    public FontWeight Peso { get => (FontWeight)GetValue(PesoProperty); set => SetValue(PesoProperty, value); }
    public double Espacamento { get => (double)GetValue(EspacamentoProperty); set => SetValue(EspacamentoProperty, value); }
    public Brush Cor { get => (Brush)GetValue(CorProperty); set => SetValue(CorProperty, value); }
    public bool SoMaiusculas { get => (bool)GetValue(SoMaiusculasProperty); set => SetValue(SoMaiusculasProperty, value); }

    enum Sinal { Nenhum, Agudo, Grave, Circunflexo, Til, Cedilha, Trema }

    // Largura e InkEsq vêm do desenho da letra (não da largura "oficial" da fonte): assim o vão
    // entre uma letra e outra é igual, até em volta de letra estreita como o I.
    sealed record Peca(FormattedText? Texto, double Largura, double InkEsq, Sinal Sinal, bool Colada);

    List<Peca>? _pecas;
    double _largura, _altura, _baseline, _alturaMaiuscula;

    void Montar()
    {
        if (_pecas != null) return;
        _pecas = new List<Peca>();
        var tf = new Typeface(Fonte, FontStyles.Normal, Peso, FontStretches.Normal);
        tf.TryGetGlyphTypeface(out var glifos);
        double dpi = 1.0;
        try { dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip; } catch { }

        bool Tem(char c) => glifos == null || glifos.CharacterToGlyphMap.ContainsKey(c);

        foreach (var c in Texto ?? "")
        {
            var letra = c.ToString();
            var sinal = Sinal.Nenhum;
            if (!Tem(c))
            {
                var dec = c.ToString().Normalize(NormalizationForm.FormD);
                if (dec.Length >= 2 && Tem(dec[0]))
                {
                    letra = dec[0].ToString();
                    sinal = dec[1] switch
                    {
                        '́' => Sinal.Agudo,
                        '̀' => Sinal.Grave,
                        '̂' => Sinal.Circunflexo,
                        '̃' => Sinal.Til,
                        '̧' => Sinal.Cedilha,
                        '̈' => Sinal.Trema,
                        _ => Sinal.Nenhum,
                    };
                }
            }
            if (c == ' ')
            {
                _pecas.Add(new Peca(null, Tamanho * 0.3, 0, Sinal.Nenhum, false));
                continue;
            }
            var ft = new FormattedText(letra, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, Tamanho, Cor, dpi);
            var tinta = ft.BuildGeometry(new Point(0, 0)).Bounds;
            if (tinta.IsEmpty || tinta.Width <= 0) tinta = new Rect(0, 0, ft.WidthIncludingTrailingWhitespace, 1);
            // vírgula e ponto grudam na letra de antes
            _pecas.Add(new Peca(ft, tinta.Width, tinta.Left, sinal, c is ',' or '.'));
        }

        _largura = 0;
        for (int i = 0; i < _pecas.Count; i++) _largura += Vao(i) + _pecas[i].Largura;

        double baseline = glifos != null ? glifos.Baseline * Tamanho : Tamanho * 0.8;
        _alturaMaiuscula = glifos != null ? glifos.CapsHeight * Tamanho : Tamanho * 0.7;
        if (SoMaiusculas)
        {
            // em cima cabe o agudo/circunflexo improvisado; embaixo, o traço da cedilha
            double folgaCima = Tamanho * 0.3, folgaBaixo = Tamanho * 0.24;
            _altura = _alturaMaiuscula + folgaCima + folgaBaixo;
            _baseline = folgaCima + _alturaMaiuscula;
            // deslocamento do FormattedText: ele desenha a partir do topo da linha
            _desloc = _baseline - baseline;
        }
        else
        {
            var lh = glifos != null ? glifos.Height * Tamanho : Tamanho * 1.2;
            _altura = lh;
            _baseline = baseline;
            _desloc = 0;
        }
    }

    double _desloc;

    // espaço antes da peça i
    double Vao(int i)
    {
        if (i == 0 || _pecas == null) return 0;
        if (_pecas[i].Colada) return Espacamento * Tamanho * 0.15;
        return Espacamento * Tamanho;
    }

    protected override Size MeasureOverride(Size disponivel)
    {
        Montar();
        return new Size(_largura, _altura);
    }

    protected override void OnRender(DrawingContext dc)
    {
        Montar();
        if (_pecas == null) return;
        double x = Math.Max(0, (ActualWidth - _largura) / 2);
        double traco = Math.Max(1.2, Tamanho * 0.075);
        var caneta = new Pen(Cor, traco) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
        caneta.Freeze();

        for (int i = 0; i < _pecas.Count; i++)
        {
            var p = _pecas[i];
            x += Vao(i);
            if (p.Texto != null)
            {
                dc.DrawText(p.Texto, new Point(x - p.InkEsq, _desloc));
                if (p.Sinal != Sinal.Nenhum) DesenharSinal(dc, caneta, p.Sinal, x, p.Largura);
            }
            x += p.Largura;
        }
    }

    void DesenharSinal(DrawingContext dc, Pen caneta, Sinal sinal, double x, double largura)
    {
        double cx = x + largura / 2;
        double topo = _baseline - _alturaMaiuscula;
        double s = Tamanho;
        double acima = topo - s * 0.09;
        switch (sinal)
        {
            case Sinal.Cedilha:
                // risco na diagonal embaixo do C (pedido dele), na mesma inclinação do agudo
                dc.DrawLine(caneta, new Point(cx - s * 0.08, _baseline + s * 0.19), new Point(cx + s * 0.08, _baseline + s * 0.05));
                break;
            case Sinal.Agudo:
                dc.DrawLine(caneta, new Point(cx - s * 0.08, acima), new Point(cx + s * 0.1, acima - s * 0.11));
                break;
            case Sinal.Grave:
                dc.DrawLine(caneta, new Point(cx + s * 0.08, acima), new Point(cx - s * 0.1, acima - s * 0.11));
                break;
            case Sinal.Circunflexo:
                dc.DrawLine(caneta, new Point(cx - s * 0.12, acima), new Point(cx, acima - s * 0.1));
                dc.DrawLine(caneta, new Point(cx, acima - s * 0.1), new Point(cx + s * 0.12, acima));
                break;
            case Sinal.Til:
                dc.DrawLine(caneta, new Point(cx - s * 0.14, acima - s * 0.05), new Point(cx + s * 0.14, acima - s * 0.05));
                break;
            case Sinal.Trema:
                dc.DrawLine(caneta, new Point(cx - s * 0.12, acima - s * 0.05), new Point(cx - s * 0.04, acima - s * 0.05));
                dc.DrawLine(caneta, new Point(cx + s * 0.04, acima - s * 0.05), new Point(cx + s * 0.12, acima - s * 0.05));
                break;
        }
    }
}
