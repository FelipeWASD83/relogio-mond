using System;
using System.Windows;
using System.Windows.Media;

namespace RelogioMond;

// As barras. Cada quadro recebe o espectro cru e suaviza: sobe rápido, desce devagar,
// com um pontinho de pico que cai depois de segurar um instante.
public sealed class Visualizador : FrameworkElement
{
    float[] _alvo = Array.Empty<float>();
    float[] _valor = Array.Empty<float>();
    float[] _pico = Array.Empty<float>();
    double[] _seguraPico = Array.Empty<double>();
    Brush _cor = Brushes.White;
    Brush _corPico = Brushes.White;

    public int Quantidade
    {
        get => _valor.Length;
        set
        {
            if (value == _valor.Length) return;
            _alvo = new float[value];
            _valor = new float[value];
            _pico = new float[value];
            _seguraPico = new double[value];
            InvalidateVisual();
        }
    }

    public float[] Entrada => _alvo;

    public Visualizador()
    {
        Quantidade = 56;
        var c = new SolidColorBrush(Color.FromArgb(235, 255, 255, 255)); c.Freeze(); _cor = c;
        var p = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)); p.Freeze(); _corPico = p;
    }

    // dt em segundos. Devolve true se ainda tem barra em movimento (vale redesenhar).
    public bool Passo(double dt)
    {
        bool mexe = false;
        dt = Math.Clamp(dt, 0.001, 0.1);
        double subida = 1 - Math.Pow(0.0005, dt);
        double descida = 1 - Math.Pow(0.04, dt);
        for (int i = 0; i < _valor.Length; i++)
        {
            float alvo = _alvo[i];
            float v = _valor[i];
            v += (float)((alvo - v) * (alvo > v ? subida : descida));
            if (v < 0.002f) v = 0;
            if (Math.Abs(v - _valor[i]) > 0.0005f) mexe = true;
            _valor[i] = v;

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
