using System;
using System.Windows;
using System.Windows.Media;

namespace RelogioMond;

// As barras, com o mesmo movimento do visualizador da ilha: o espectro é lido ~30 vezes por segundo
// (Amostrar), a barra sobe na hora e desce devagar (0,09 por leitura), cada leitura nova é percorrida em
// linha reta em 70 ms (a animação implícita da ilha) e a altura cresce a partir do centro, com piso de 20%.
public sealed class Visualizador : FrameworkElement
{
    public const double IntervaloAmostra = 1.0 / 30;
    const double TempoInterpolacao = 0.070;
    const float Piso = 0.2f;
    const float Queda = 0.09f;

    float[] _alvo = Array.Empty<float>();   // entrada: o espectro cru
    float[] _suave = Array.Empty<float>();  // depois da subida na hora e da queda lenta
    float[] _de = Array.Empty<float>();     // onde a barra estava quando chegou a leitura
    float[] _valor = Array.Empty<float>();  // o que está desenhado
    double _t = TempoInterpolacao;
    readonly Brush _cor;

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
            InvalidateVisual();
        }
    }

    public float[] Entrada => _alvo;
    public float Valor(int i) => _valor[i];

    public Visualizador()
    {
        Quantidade = 56;
        var c = new SolidColorBrush(Color.FromArgb(235, 255, 255, 255)); c.Freeze(); _cor = c;
    }

    // Uma leitura nova do espectro (já em Entrada): ~30 vezes por segundo.
    public void Amostrar()
    {
        for (int i = 0; i < _valor.Length; i++)
        {
            float alvo = float.IsFinite(_alvo[i]) ? Math.Clamp(_alvo[i], 0, 1) : 0;
            _suave[i] = alvo >= _suave[i] ? alvo : Math.Max(alvo, _suave[i] - Queda);
            _de[i] = _valor[i];
        }
        _t = 0;
    }

    // Derruba tudo de uma vez (música parou, janela coberta): sem esperar a queda lenta.
    public void Zerar()
    {
        Array.Clear(_alvo);
        Array.Clear(_suave);
        Array.Clear(_de);
        Array.Clear(_valor);
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
        }
        return mexe;
    }

    protected override void OnRender(DrawingContext dc)
    {
        int n = _valor.Length;
        if (n == 0 || ActualWidth <= 0) return;
        double passo = ActualWidth / n;
        double largura = Math.Max(1, passo * 0.5);
        double h = ActualHeight;
        double raio = largura / 2;
        for (int i = 0; i < n; i++)
        {
            double x = i * passo + (passo - largura) / 2;
            double altura = Math.Max(1, (Piso + (1 - Piso) * _valor[i]) * h);
            double r = Math.Min(raio, altura / 2);
            dc.DrawRoundedRectangle(_cor, null, new Rect(x, (h - altura) / 2, largura, altura), r, r);
        }
    }
}
