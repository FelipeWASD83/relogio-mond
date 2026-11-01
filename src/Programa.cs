using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace RelogioMond;

public static class Programa
{
    [STAThread]
    public static int Main(string[] args)
    {
        // o dotnet.exe não declara DPI no manifesto: sem isto o relógio sairia borrado a 125%
        Nativo.SetProcessDpiAwarenessContext(Nativo.DPI_PER_MONITOR_V2);

        string? Arg(string nome)
        {
            int i = Array.IndexOf(args, nome);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        if (args.Contains("--diagnostico")) return Diagnostico();
        if (args.Contains("--testes")) return Testes.Rodar();
        if (args.Contains("--tocar-mudo")) return Testes.TocarMudo();
        if (args.Contains("--captura")) return Captura(Arg("--captura")!, Arg("--idioma"), Arg("--data"), Arg("--escala"), args.Contains("--controles"), args.Contains("--sem-midia"));

        using var unico = new Mutex(true, @"Local\RelogioMond", out bool primeiro);
        if (!primeiro && !args.Contains("--medir")) return 0;

        var app = new App(args.Contains("--demo"), Arg("--medir"));
        return app.Run();
    }

    // Mostra no console o que o relógio está lendo agora: música e nível do som.
    static int Diagnostico()
    {
        var midia = new Midia();
        midia.Iniciar().GetAwaiter().GetResult();
        for (int i = 0; i < 3; i++) { midia.Atualizar().GetAwaiter().GetResult(); Thread.Sleep(400); }
        var e = midia.Estado();
        Console.WriteLine(e.Tem
            ? $"midia: \"{e.Titulo}\" de \"{e.Artista}\" tocando={e.Tocando} {e.Posicao:m\\:ss}/{e.Duracao:m\\:ss} capa={(e.Capa != null ? $"{e.Capa.PixelWidth}x{e.Capa.PixelHeight}" : "nenhuma")}"
            : "midia: nenhuma sessão com título");
        using var audio = new CapturaAudio();
        audio.Ligar();
        var faixas = new float[24];
        float maior = 0;
        for (int i = 0; i < 20; i++)
        {
            Thread.Sleep(100);
            maior = Math.Max(maior, audio.Nivel());
            audio.Espectro(faixas);
        }
        Console.WriteLine($"audio: nivel_max={maior:0.0000} faixas=[{string.Join(" ", faixas.Select(f => (int)(f * 9)))}]");
        return 0;
    }

    // Renderiza o mostrador num PNG, sem abrir janela, sobre um fundo escuro qualquer.
    static int Captura(string arquivo, string? idioma, string? data, string? escala, bool controles, bool semMidia)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        int codigo = 0;
        app.Startup += (_, _) =>
        {
            try
            {
                var cfg = new Config();
                Idioma.Definir(idioma ?? "pt");
                var quando = data != null ? DateTime.ParseExact(data, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : DateTime.Now;
                var esc = escala != null ? double.Parse(escala, CultureInfo.InvariantCulture) : 1.0;
                var m = new Mostrador();
                m.AplicarEscala(esc);
                m.Barras.Quantidade = cfg.Barras;
                m.AtualizarRelogio(quando, true, idioma == "en" ? "23°  PARTLY CLOUDY" : "23°  PARCIALMENTE NUBLADO");
                if (!semMidia)
                {
                    m.AtualizarMidia(Demo.Estado());
                    Demo.Espectro(m.Barras.Entrada, 1.7);
                    for (int i = 0; i < 40; i++) m.Barras.Passo(0.1);
                }
                m.PularAnimacoes(controles);

                const double dpi = 120; // 125% do monitor dele
                var fundo = new Grid { Background = new LinearGradientBrush(Color.FromRgb(0x1D, 0x2B, 0x3A), Color.FromRgb(0x4A, 0x3B, 0x52), 30) };
                fundo.Children.Add(m);
                fundo.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                fundo.Arrange(new Rect(fundo.DesiredSize));
                fundo.UpdateLayout();
                if (!semMidia) m.AtualizarMidia(Demo.Estado()); // a barra de progresso precisa da largura já medida
                fundo.UpdateLayout();
                int w = (int)Math.Ceiling(fundo.ActualWidth * dpi / 96), h = (int)Math.Ceiling(fundo.ActualHeight * dpi / 96);
                var rtb = new RenderTargetBitmap(w, h, dpi, dpi, PixelFormats.Pbgra32);
                rtb.Render(fundo);
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(rtb));
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(arquivo))!);
                using (var f = File.Create(arquivo)) enc.Save(f);
                Console.WriteLine($"{arquivo} {w}x{h} anurati={m.TemFonteAnurati}");
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                codigo = 1;
            }
            app.Shutdown(codigo);
        };
        app.Run();
        return codigo;
    }
}

public sealed class App : Application
{
    readonly bool _demo;
    readonly int? _medirSegundos;
    Config _cfg = Config.Carregar();
    JanelaRelogio? _janela;
    Bandeja? _bandeja;
    JanelaAtualizacao? _janelaAtualizacao;
    FileSystemWatcher? _vigiaConfig;
    DispatcherTimer? _atrasoConfig;
    int _codigoSaida;

    public App(bool demo, string? medir)
    {
        _demo = demo;
        if (int.TryParse(medir, out var s)) _medirSegundos = s;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += (_, e) =>
        {
            Log.Escrever("erro não tratado: " + e.Exception);
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Escrever("erro fatal: " + e.ExceptionObject);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Idioma.Definir(_cfg.Idioma);
        _janela = new JanelaRelogio(_cfg) { ModoDemo = _demo };

        if (_medirSegundos is int segundos)
        {
            var medidor = new Medidor();
            _janela.Medidor = medidor;
            _janela.Show();
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(segundos) };
            t.Tick += (_, _) =>
            {
                t.Stop();
                var ps = medidor.PorSegundo.Skip(1).OrderBy(x => x).ToList(); // o primeiro segundo é aquecimento
                var mediana = ps.Count > 0 ? ps[ps.Count / 2] : 0;
                var cpu = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds / medidor.Relogio.Elapsed.TotalMilliseconds / Environment.ProcessorCount * 100;
                var texto = $"fps mediana={mediana} min={(ps.Count > 0 ? ps[0] : 0)} max={(ps.Count > 0 ? ps[^1] : 0)} segundos={ps.Count} cpu_media={cpu:0.00}% memoria={Environment.WorkingSet / 1048576}MB {_janela.Diagnostico()}";
                File.WriteAllText(Path.Combine(Caminhos.Dados, "medicao.txt"), texto);
                Shutdown(0);
            };
            t.Start();
            return;
        }

        InicioAutomatico.Aplicar(_cfg.IniciarComWindows);
        _janela.Show();
        // sinal de vida para o iniciar.pyw: a versão abriu (não precisa voltar para a anterior)
        try { Directory.CreateDirectory(Caminhos.Atualizacao); File.WriteAllText(Path.Combine(Caminhos.Atualizacao, "iniciou"), Atualizador.VersaoAtual()); }
        catch (Exception ex) { Log.Escrever("sinal de vida: " + ex.Message); }
        _bandeja = new Bandeja(() => _cfg, Salvar, Recarregar, ProcurarAtualizacao, () => Sair(0));
        if (!File.Exists(Path.Combine(Caminhos.Fontes, "Anurati-Regular.otf")))
            _bandeja.Avisar(Idioma.T("sem_fonte"));
        var recado = Atualizador.RecadoDoLancador();
        if (recado != null) _bandeja.Avisar(recado);
        VigiarConfig();
    }

    void Salvar(Config c)
    {
        _cfg = c;
        _cfg.Salvar(); // o vigia do arquivo também dispara, mas aplicar duas vezes não faz mal
        Aplicar();
    }

    void Aplicar()
    {
        Idioma.Definir(_cfg.Idioma);
        InicioAutomatico.Aplicar(_cfg.IniciarComWindows);
        _janela?.Aplicar(_cfg);
        _bandeja?.Montar();
    }

    void Recarregar()
    {
        _cfg = Config.Carregar();
        Aplicar();
    }

    void VigiarConfig()
    {
        try
        {
            Directory.CreateDirectory(Caminhos.Dados);
            _atrasoConfig = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _atrasoConfig.Tick += (_, _) => { _atrasoConfig.Stop(); Recarregar(); };
            _vigiaConfig = new FileSystemWatcher(Caminhos.Dados, "config.json") { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size };
            _vigiaConfig.Changed += (_, _) => Dispatcher.BeginInvoke(() => { _atrasoConfig.Stop(); _atrasoConfig.Start(); });
            _vigiaConfig.EnableRaisingEvents = true;
        }
        catch (Exception e)
        {
            Log.Escrever("vigia do config.json: " + e.Message);
        }
    }

    void ProcurarAtualizacao()
    {
        if (_janelaAtualizacao != null) { _janelaAtualizacao.Activate(); return; }
        _janelaAtualizacao = new JanelaAtualizacao();
        _janelaAtualizacao.Closed += (_, _) => _janelaAtualizacao = null;
        _janelaAtualizacao.Pronto += () => Sair(Atualizador.CodigoAplicar);
        _janelaAtualizacao.Show();
    }

    void Sair(int codigo)
    {
        _codigoSaida = codigo;
        _vigiaConfig?.Dispose();
        _bandeja?.Dispose();
        _janela?.Close();
        Shutdown(codigo);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _bandeja?.Dispose();
        e.ApplicationExitCode = _codigoSaida != 0 ? _codigoSaida : e.ApplicationExitCode;
        base.OnExit(e);
    }
}
