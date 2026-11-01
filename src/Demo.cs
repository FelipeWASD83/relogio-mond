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

    public static EstadoMidia Estado(bool tocando = true) => new()
    {
        Tem = true,
        Titulo = Idioma.Ingles ? "Test Track (Night Drive)" : "Faixa de Teste (Noite na Estrada)",
        Artista = "Relógio Mond",
        Capa = Capa(),
        Tocando = tocando,
        Posicao = TimeSpan.FromSeconds(83 + (DateTime.Now - Inicio).TotalSeconds),
        Duracao = TimeSpan.FromSeconds(224),
    };

    // Áudio sintético parecido com música (graves num pulso de 2 Hz, médios e agudos oscilando,
    // menos energia para o agudo), para passar pelo mesmo caminho do áudio real: FFT, faixas e ganho.
    public static float[] Amostras(double t0, int n, int taxa)
    {
        var s = new float[n];
        for (int i = 0; i < n; i++)
        {
            double t = t0 + (double)i / taxa;
            double soma = 0;
            for (int k = 0; k < 29; k++)
            {
                double f = 55 * Math.Pow(2, k / 4.0);
                double ritmo = k < 8
                    ? Math.Pow(Math.Abs(Math.Sin(Math.PI * 2.0 * t)), 6)
                    : 0.35 + 0.65 * Math.Pow(Math.Abs(Math.Sin(2 * Math.PI * (0.7 + 0.23 * (k % 7)) * t + k)), 2);
                soma += 0.05 * Math.Pow(f / 110, -0.7) * ritmo * Math.Sin(2 * Math.PI * f * t);
            }
            s[i] = (float)soma;
        }
        return s;
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
