using System;
using System.Windows;
using System.Windows.Media;

namespace RelogioMond;

// As barras. O visual é o da primeira versão: barras finas assentadas embaixo, cada uma com um pontinho de pico
// que fica um instante e cai devagar. O movimento é o do visualizador da ilha: o espectro é lido ~30 vezes por
// segundo (Amostrar), a barra sobe na hora e desce devagar (0,09 por leitura) e cada leitura nova é percorrida
// em linha reta em 70 ms.
public sealed class Visualizador : FrameworkElement
{
    public const double IntervaloAmostra = 1.0 / 30;
    const double TempoInterpolacao = 0.070;
    const float Queda = 0.09f;
    const float Subida = 0.7f;   // parte da distância que a barra sobe a cada leitura (antes: tudo, de uma vez)

    float[] _alvo = Array.Empty<float>();   // entrada: o espectro cru
    float[] _suave = Array.Empty<float>();  // depois da subida na hora e da queda lenta
    float[] _de = Array.Empty<float>();     // onde a barra estava quando chegou a leitura
    float[] _valor = Array.Empty<float>();  // o que está desenhado
    float[] _pico = Array.Empty<float>();
    float[] _vizinhas = Array.Empty<float>();
    double[] _seguraPico = Array.Empty<double>();
    double _t = TempoInterpolacao;
    readonly Brush _cor;
    readonly Brush _corPico;

    public int Quantidade
    {
        get => _valor.Length;
        set
        {
            if (value == _valor.Length) return;
            _alvo = new float[value];
            _suave = new float[value];
            _de = new float[value];
            _valor = new float[value];
            _pico = new float[value];
            _vizinhas = new float[value];
            _seguraPico = new double[value];
            InvalidateVisual();
        }
    }

    public float[] Entrada => _alvo;
    public float Valor(int i) => _valor[i];

    public Visualizador()
    {
        Quantidade = 56;
        var c = new SolidColorBrush(Color.FromArgb(235, 255, 255, 255)); c.Freeze(); _cor = c;
        var p = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)); p.Freeze(); _corPico = p;
    }

    // Uma leitura nova do espectro (já em Entrada): ~30 vezes por segundo.
    public void Amostrar()
    {
        int n = _valor.Length;
        // a silhueta fica contínua: cada barra recebe um pouco de cada vizinha (1-2-1), senão as barras
        // ao lado ficam desiguais e o conjunto parece serrilhado
        for (int i = 0; i < n; i++)
        {
            float a = Limpar(_alvo[i]);
            float e = i > 0 ? Limpar(_alvo[i - 1]) : a, d = i < n - 1 ? Limpar(_alvo[i + 1]) : a;
            _vizinhas[i] = Math.Min(1f, (0.5f * a + 0.25f * (e + d)) * 1.06f);
        }
        for (int i = 0; i < n; i++)
        {
            float alvo = _vizinhas[i];
            _suave[i] = alvo >= _suave[i] ? _suave[i] + (alvo - _suave[i]) * Subida : Math.Max(alvo, _suave[i] - Queda);
            _de[i] = _valor[i];
        }
        _t = 0;
    }

    static float Limpar(float v) => float.IsFinite(v) ? Math.Clamp(v, 0, 1) : 0;

    // Derruba tudo de uma vez (música parou, janela coberta): sem esperar a queda lenta.
    public void Zerar()
    {
        Array.Clear(_alvo);
        Array.Clear(_suave);
        Array.Clear(_de);
        Array.Clear(_valor);
        Array.Clear(_pico);
        Array.Clear(_seguraPico);
        _t = TempoInterpolacao;
        InvalidateVisual();
    }

    // dt em segundos. Devolve true se ainda tem barra em movimento (vale redesenhar).
    public bool Passo(double dt)
    {
        dt = Math.Clamp(dt, 0.001, 0.1);
        _t = Math.Min(_t + dt, TempoInterpolacao);
        float f = (float)(_t / TempoInterpolacao);
        bool mexe = _t < TempoInterpolacao;
        for (int i = 0; i < _valor.Length; i++)
        {
            float v = _de[i] + (_suave[i] - _de[i]) * f;
            if (Math.Abs(v - _valor[i]) > 0.0005f || Math.Abs(_suave[i] - v) > 0.0005f) mexe = true;
            _valor[i] = v;

            // pontinho de pico: segura 0,35 s no ponto mais alto e cai devagar
            if (v >= _pico[i]) { _pico[i] = v; _seguraPico[i] = 0.35; }
            else if (_seguraPico[i] > 0) _seguraPico[i] -= dt;
            else _pico[i] = Math.Max(v, _pico[i] - (float)(0.9 * dt));
            if (_pico[i] > 0.002f) mexe = true;
        }
        return mexe;
    }

    protected override void OnRender(DrawingContext dc)
    {
        int n = _valor.Length;
        if (n == 0 || ActualWidth <= 0) return;
        double passo = ActualWidth / n;
        double largura = Math.Max(1, passo * 0.56);
        double h = ActualHeight;
        double raio = largura / 2;
        double pontinho = Math.Max(1.5, largura * 0.5);
        for (int i = 0; i < n; i++)
        {
            double x = i * passo + (passo - largura) / 2;
            double altura = Math.Max(largura, _valor[i] * (h - pontinho * 2.5));
            dc.DrawRoundedRectangle(_cor, null, new Rect(x, h - altura, largura, altura), raio, raio);
            if (_pico[i] > 0.02f)
            {
                double yp = h - Math.Max(largura, _pico[i] * (h - pontinho * 2.5)) - pontinho * 1.8;
                dc.DrawRoundedRectangle(_corPico, null, new Rect(x, yp, largura, pontinho), pontinho / 2, pontinho / 2);
            }
        }
    }
}
