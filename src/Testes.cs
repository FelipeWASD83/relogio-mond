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

        // um NaN vindo do driver não pode matar as barras: o ganho automático guardava o NaN para sempre
        using (var a = new CapturaAudio())
        {
            const int taxa = 48000, n = 56;
            var f = new float[n];
            var sujo = Enumerable.Range(0, 8192).Select(i => i % 500 == 0 ? float.NaN : (float)(0.5 * Math.Sin(2 * Math.PI * 1000 * i / taxa))).ToArray();
            a.Alimentar(sujo, taxa);
            a.Espectro(f);
            var seno = Enumerable.Range(0, 8192).Select(i => (float)(0.5 * Math.Sin(2 * Math.PI * 1000 * i / taxa))).ToArray();
            a.Alimentar(seno, taxa);
            a.Espectro(f);
            Conferir(f.All(float.IsFinite) && f.Max() >= 0.95, "NaN do driver não envenena as barras", $"maior={f.Max():0.00}");
        }

        // o movimento das barras é o da ilha: sobe na hora (percorrido em 70 ms) e desce 0,09 por leitura
        {
            var v = new Visualizador { Quantidade = 4 };
            v.Entrada[0] = 1;
            v.Amostrar();
            v.Passo(0.035);
            float meio = v.Valor(0);
            v.Passo(0.035);
            float cheio = v.Valor(0);
            v.Entrada[0] = 0;
            v.Amostrar();
            v.Passo(0.07);
            float caiu = v.Valor(0);
            Conferir(Math.Abs(meio - 0.5f) < 0.02f && cheio > 0.99f && Math.Abs(caiu - 0.91f) < 0.01f,
                "barras com o movimento da ilha", $"35 ms={meio:0.00} 70 ms={cheio:0.00} depois de 1 leitura de queda={caiu:0.00} (esperado 0,50 / 1,00 / 0,91)");
        }

        // a captura real, com o contexto de sincronia do WPF (é nele que o NAudio avisa que a captura antiga parou):
        // depois de fechar e abrir de novo, ela não pode se reabrir sozinha em loop
        {
            int aberturas = -1;
            var th = new System.Threading.Thread(() =>
            {
                var disp = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                System.Threading.SynchronizationContext.SetSynchronizationContext(
                    new System.Windows.Threading.DispatcherSynchronizationContext(disp));
                var a = new CapturaAudio();
                a.Ligar();
                a.Desligar();
                a.Ligar();
                var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                t.Tick += (_, _) => a.Garantir();
                t.Start();
                var fim = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(7) };
                fim.Tick += (_, _) => { aberturas = a.Aberturas; a.Dispose(); disp.InvokeShutdown(); };
                fim.Start();
                System.Windows.Threading.Dispatcher.Run();
            });
            th.SetApartmentState(System.Threading.ApartmentState.STA);
            th.Start();
            th.Join();
            Conferir(aberturas is >= 0 and <= 2, "captura não se reabre em loop", $"{aberturas} aberturas em 7 s (esperado 2: a primeira e a de depois do Desligar/Ligar)");
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

            // pulo na minutagem: o destino aparece na hora e a sessão do player confirma
            conferir(midia.Estado().PodeBuscar, "player aceita pulo na minutagem", "PodeBuscar=" + midia.Estado().PodeBuscar);
            midia.Buscar(TimeSpan.FromSeconds(20));
            var logo = midia.Estado().Posicao;
            conferir((logo - TimeSpan.FromSeconds(20)).Duration() < TimeSpan.FromSeconds(1), "destino do pulo na hora", $"{logo:m\\:ss\\.f} (esperado ~0:20)");
            await Task.Delay(1500);
            await midia.Atualizar();
            var depois = midia.Estado().Posicao;
            conferir((depois - TimeSpan.FromSeconds(21.5)).Duration() < TimeSpan.FromSeconds(2.5), "player pulou de verdade", $"{depois:m\\:ss\\.f} (esperado ~0:21)");

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
    public static int TocarMudo(string? titulo = null)
    {
        Task.Run(async () =>
        {
            var wav = Path.Combine(Path.GetTempPath(), titulo == null ? "relogio-mond-mudo.wav" : "relogio-mond-mudo-b.wav");
            EscreverWavMudo(wav, 30);
            var player = new MediaPlayer { Volume = 0, IsMuted = true };
            var item = new MediaPlaybackItem(MediaSource.CreateFromStorageFile(await StorageFile.GetFileFromPathAsync(wav)));
            var props = item.GetDisplayProperties();
            props.Type = MediaPlaybackType.Music;
            props.MusicProperties.Title = titulo ?? TituloTeste;
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
