using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RelogioMond;

// Música e espectro de mentira, para a captura em PNG e a medição de fps sem depender
// de tocar som no PC.
public static class Demo
{
    static BitmapSource? _capa;
    static readonly DateTime Inicio = DateTime.Now;

    public static EstadoMidia Estado() => new()
    {
        Tem = true,
        Titulo = Idioma.Ingles ? "Test Track (Night Drive)" : "Faixa de Teste (Noite na Estrada)",
        Artista = "Relógio Mond",
        Capa = Capa(),
        Tocando = true,
        Posicao = TimeSpan.FromSeconds(83 + (DateTime.Now - Inicio).TotalSeconds),
        Duracao = TimeSpan.FromSeconds(224),
    };

    public static void Espectro(float[] saida, double t)
    {
        int n = saida.Length;
        for (int i = 0; i < n; i++)
        {
            double x = (double)i / n;
            double forma = 0.85 * Math.Exp(-x * 1.6) + 0.15;
            double mexe = 0.55 + 0.45 * Math.Sin(t * 7.3 + i * 0.55) * Math.Sin(t * 2.1 + i * 0.13);
            saida[i] = (float)Math.Clamp(forma * mexe, 0, 1);
        }
    }

    public static BitmapSource Capa()
    {
        if (_capa != null) return _capa;
        var v = new DrawingVisual();
        using (var dc = v.RenderOpen())
        {
            var fundo = new LinearGradientBrush(Color.FromRgb(0xE8, 0x6A, 0x3A), Color.FromRgb(0x5B, 0x2A, 0x86), 45);
            dc.DrawRectangle(fundo, null, new Rect(0, 0, 300, 300));
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(200, 0xFF, 0xD2, 0x7A)), null, new Point(150, 175), 70, 70);
            for (int i = 0; i < 6; i++)
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x5B, 0x2A, 0x86)), null, new Rect(60, 180 + i * 14, 180, 4 + i));
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x1E, 0x10, 0x30)), null, new Rect(0, 240, 300, 60));
        }
        var bmp = new RenderTargetBitmap(300, 300, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(v);
        bmp.Freeze();
        return _capa = bmp;
    }
}
