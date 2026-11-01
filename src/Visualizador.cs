using System;
using System.Windows;
using System.Windows.Media;

namespace RelogioMond;

// As barras. Cada quadro recebe o espectro cru e suaviza: sobe rápido, desce devagar.
// Sem pontinho de pico desde a 1.0.2: com as barras baixas ele comia a altura toda.
public sealed class Visualizador : FrameworkElement
{
    float[] _alvo = Array.Empty<float>();
    float[] _valor = Array.Empty<float>();
    readonly Brush _cor;

    public int Quantidade
    {
        get => _valor.Length;
        set
        {
            if (value == _valor.Length) return;
            _alvo = new float[value];
            _valor = new float[value];
            InvalidateVisual();
        }
    }

    public float[] Entrada => _alvo;

    public Visualizador()
    {
        Quantidade = 56;
        var c = new SolidColorBrush(Color.FromArgb(235, 255, 255, 255)); c.Freeze(); _cor = c;
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
            double altura = Math.Max(largura * 0.6, _valor[i] * h);
            dc.DrawRoundedRectangle(_cor, null, new Rect(x, h - altura, largura, altura), Math.Min(raio, altura / 2), Math.Min(raio, altura / 2));
        }
    }
}
