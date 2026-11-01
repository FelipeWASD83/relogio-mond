using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Streams;

namespace RelogioMond;

// Testes que rodam sem soltar som: `dotnet app\RelogioMond.dll --testes`.
public static class Testes
{
    const string TituloTeste = "Faixa Muda de Teste";

    public static int Rodar()
    {
        int falhas = 0;
        void Conferir(bool ok, string nome, string detalhe)
        {
            Console.WriteLine($"{(ok ? "ok  " : "FALHOU")} {nome}: {detalhe}");
            if (!ok) falhas++;
        }

        // 1. espectro: um seno de 1 kHz tem de acender a faixa de 1 kHz e quase nada longe dela
        using (var a = new CapturaAudio())
        {
            const int taxa = 48000, n = 56;
            var seno = Enumerable.Range(0, 8192).Select(i => (float)(0.5 * Math.Sin(2 * Math.PI * 1000 * i / taxa))).ToArray();
            a.Alimentar(seno, taxa);
            var f = new float[n];
            a.Espectro(f);
            int pico = Array.IndexOf(f, f.Max());
            double f0 = 40 * Math.Pow(16000 / 40.0, (double)pico / n), f1 = 40 * Math.Pow(16000 / 40.0, (double)(pico + 1) / n);
            Conferir(f0 <= 1000 && 1000 <= f1 * 1.15, "faixa do seno de 1 kHz", $"pico na barra {pico} ({f0:0}-{f1:0} Hz) valor {f[pico]:0.00}");
            Conferir(f[2] < 0.15 && f[n - 3] < 0.15, "graves e agudos apagados", $"barra 2={f[2]:0.00} barra {n - 3}={f[n - 3]:0.00}");
            a.Alimentar(new float[8192], taxa);
            a.Espectro(f);
            Conferir(f.Max() < 0.02, "silêncio zera as barras", $"maior={f.Max():0.000}");
        }

        // música bem baixa (cinco tons a -50 dB cada) tem de encher as barras do mesmo jeito: ganho automático
        using (var a = new CapturaAudio())
        {
            const int taxa = 48000, n = 56;
            var tons = new[] { 100, 300, 1000, 3000, 8000 };
            var baixa = Enumerable.Range(0, 8192).Select(i => (float)tons.Sum(t => 0.003 * Math.Sin(2 * Math.PI * t * i / taxa))).ToArray();
            a.Alimentar(baixa, taxa);
            var f = new float[n];
            a.Espectro(f);
            Conferir(f.Max() >= 0.95, "som baixo enche as barras", $"maior={f.Max():0.00} (sem o ganho automático ficava em ~0.7)");
        }

        // 2. idioma
        Idioma.Definir("pt");
        var terca = new DateTime(2026, 11, 3, 18, 42, 0);
        Conferir(Idioma.Dia(terca) == "TERÇA" && Idioma.Data(terca) == "3 DE NOVEMBRO, 2026", "português", $"{Idioma.Dia(terca)} / {Idioma.Data(terca)}");
        Idioma.Definir("en");
        Conferir(Idioma.Dia(terca) == "TUESDAY" && Idioma.Data(terca) == "NOVEMBER 3, 2026" && Idioma.Hora(terca, false) == "6:42 PM",
            "inglês", $"{Idioma.Dia(terca)} / {Idioma.Data(terca)} / {Idioma.Hora(terca, false)}");
        Idioma.Definir("pt");

        // 3. controle de mídia do Windows: um player mudo, em outro processo (como o Spotify),
        // com título e capa, tem de aparecer; e o botão de pausa do relógio tem de pausar ele.
        Task.Run(() => TesteMidia(Conferir)).GetAwaiter().GetResult();

        Console.WriteLine(falhas == 0 ? "tudo certo" : $"{falhas} falha(s)");
        return falhas == 0 ? 0 : 1;
    }

    static async Task TesteMidia(Action<bool, string, string> conferir)
    {
        var dll = Path.Combine(AppContext.BaseDirectory, "RelogioMond.dll");
        var filho = Process.Start(new ProcessStartInfo("dotnet", $"\"{dll}\" --tocar-mudo") { UseShellExecute = false, CreateNoWindow = true })!;
        try
        {
            var midia = new Midia();
            await midia.Iniciar();
            EstadoMidia e = new();
            for (int i = 0; i < 60; i++)
            {
                await midia.Atualizar();
                e = midia.Estado();
                if (e.Titulo == TituloTeste && e.Capa != null && e.Duracao > TimeSpan.Zero && e.Tocando) break;
                await Task.Delay(250);
            }
            conferir(e.Titulo == TituloTeste && e.Artista == "Relógio Mond", "título e artista", $"\"{e.Titulo}\" / \"{e.Artista}\"");
            conferir(e.Capa != null, "capa", e.Capa != null ? $"{e.Capa.PixelWidth}x{e.Capa.PixelHeight}" : "nenhuma");
            conferir(e.Duracao.TotalSeconds is > 29 and < 31, "duração", e.Duracao.ToString(@"m\:ss"));
            var p1 = midia.Estado().Posicao;
            await Task.Delay(1500);
            await midia.Atualizar();
            var p2 = midia.Estado().Posicao;
            conferir(e.Tocando && p2 - p1 > TimeSpan.FromSeconds(1), "minutagem andando", $"{p1:m\\:ss\\.f} -> {p2:m\\:ss\\.f}");

            midia.TocarPausar();
            bool tocando = true;
            for (int i = 0; i < 20 && tocando; i++) { await Task.Delay(200); await midia.Atualizar(); tocando = midia.Estado().Tocando; }
            conferir(!tocando, "botão de pausa", "tocando=" + tocando);
        }
        finally
        {
            try { filho.Kill(); } catch { }
        }
    }

    // O processo filho do teste: toca um WAV mudo com título, artista e capa por até 40 s.
    public static int TocarMudo()
    {
        Task.Run(async () =>
        {
            var wav = Path.Combine(Path.GetTempPath(), "relogio-mond-mudo.wav");
            EscreverWavMudo(wav, 30);
            var player = new MediaPlayer { Volume = 0, IsMuted = true };
            var item = new MediaPlaybackItem(MediaSource.CreateFromStorageFile(await StorageFile.GetFileFromPathAsync(wav)));
            var props = item.GetDisplayProperties();
            props.Type = MediaPlaybackType.Music;
            props.MusicProperties.Title = TituloTeste;
            props.MusicProperties.Artist = "Relógio Mond";
            var capa = Path.Combine(Caminhos.Raiz, "icone", "icone.png");
            if (File.Exists(capa)) props.Thumbnail = RandomAccessStreamReference.CreateFromFile(await StorageFile.GetFileFromPathAsync(capa));
            item.ApplyDisplayProperties(props);
            player.Source = item;
            player.Play();
            await Task.Delay(40000);
            player.Dispose();
        }).GetAwaiter().GetResult();
        return 0;
    }

    static void EscreverWavMudo(string arq, int segundos)
    {
        const int taxa = 8000;
        int dados = taxa * segundos * 2;
        using var w = new BinaryWriter(File.Create(arq));
        w.Write("RIFF"u8.ToArray()); w.Write(36 + dados); w.Write("WAVE"u8.ToArray());
        w.Write("fmt "u8.ToArray()); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(taxa); w.Write(taxa * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8.ToArray()); w.Write(dados); w.Write(new byte[dados]);
    }
}
