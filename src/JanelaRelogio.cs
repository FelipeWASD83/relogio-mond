using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace RelogioMond;

// A janela do relógio: sem borda, transparente, colada no fundo (atrás de todas as
// outras janelas, mas acima do papel de parede), o clique passa direto para a área de
// trabalho, e não aparece na barra de tarefas nem no Alt+Tab. Fica dona da janela da
// área de trabalho (Progman), o que a mantém visível no Win+D, como o "On Desktop" do Rainmeter.
public sealed class JanelaRelogio : Window
{
    readonly Mostrador _m = new();
    readonly Midia _midia = new();
    readonly CapturaAudio _audio = new();
    readonly Clima _clima = new();
    Config _cfg;

    IntPtr _hwnd;
    uint _msgBarraCriada;
    bool _transparenteAoClique = true;
    bool _barrasLigadas;
    bool _pausado;
    TimeSpan _ultimoQuadro;
    EstadoMidia _estado = new();
    bool _atualizandoMidia;

    readonly DispatcherTimer _tRelogio = new() { Interval = TimeSpan.FromMilliseconds(250) };
    readonly DispatcherTimer _tMouse = new() { Interval = TimeSpan.FromMilliseconds(50) };
    readonly DispatcherTimer _tVigia = new() { Interval = TimeSpan.FromSeconds(1) };
    DateTime _ultimaMidia, _ultimoClima;

    // medição de fps (modo --medir) e música de mentira (--demo)
    public Medidor? Medidor { get; set; }
    public bool ModoDemo { get; set; }

    public JanelaRelogio(Config cfg)
    {
        _cfg = cfg;
        Title = "Relógio Mond";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        Focusable = false;
        Content = _m;

        _m.PediuAnterior += _midia.Anterior;
        _m.PediuTocar += _midia.TocarPausar;
        _m.PediuProxima += _midia.Proxima;

        SourceInitialized += AoIniciarFonte;
        // o tamanho muda (clima que chega, escala, idioma): o centro continua onde a configuração manda
        SizeChanged += (_, _) => Posicionar();
        Loaded += async (_, _) =>
        {
            Aplicar(_cfg);
            await _midia.Iniciar();
            _tRelogio.Start();
            _tMouse.Start();
            _tVigia.Start();
            Tique();
        };
        Closed += (_, _) =>
        {
            DesligarBarras();
            _audio.Dispose();
        };

        _tRelogio.Tick += (_, _) => Tique();
        _tMouse.Tick += (_, _) => VerMouse();
        _tVigia.Tick += (_, _) => Vigiar();
    }

    public void Aplicar(Config cfg)
    {
        _cfg = cfg;
        Idioma.Definir(cfg.Idioma);
        _m.AplicarEscala(cfg.Escala);
        _m.Barras.Quantidade = cfg.Barras;
        _m.Opacity = cfg.Opacidade;
        _ultimoClima = DateTime.MinValue;
        Tique();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, Posicionar);
    }

    void AoIniciarFonte(object? s, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        var ex = (long)Nativo.GetWindowLongPtr(_hwnd, Nativo.GWL_EXSTYLE);
        ex |= Nativo.WS_EX_TOOLWINDOW | Nativo.WS_EX_NOACTIVATE | Nativo.WS_EX_TRANSPARENT | Nativo.WS_EX_LAYERED;
        ex &= ~Nativo.WS_EX_APPWINDOW;
        Nativo.SetWindowLongPtr(_hwnd, Nativo.GWL_EXSTYLE, new IntPtr(ex));
        PrenderNaAreaDeTrabalho();
        _msgBarraCriada = Nativo.RegisterWindowMessage("TaskbarCreated");
        HwndSource.FromHwnd(_hwnd)?.AddHook(Gancho);
    }

    void PrenderNaAreaDeTrabalho()
    {
        var progman = Nativo.FindWindow("Progman", null);
        if (progman != IntPtr.Zero) Nativo.SetWindowLongPtr(_hwnd, Nativo.GWLP_HWNDPARENT, progman);
        Nativo.SetWindowPos(_hwnd, Nativo.HWND_BOTTOM, 0, 0, 0, 0, Nativo.SWP_NOMOVE | Nativo.SWP_NOSIZE | Nativo.SWP_NOACTIVATE);
    }

    IntPtr Gancho(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool tratado)
    {
        if (msg == Nativo.WM_WINDOWPOSCHANGING)
        {
            var wp = System.Runtime.InteropServices.Marshal.PtrToStructure<Nativo.WINDOWPOS>(lParam);
            if ((wp.flags & Nativo.SWP_NOZORDER) == 0)
            {
                wp.hwndInsertAfter = Nativo.HWND_BOTTOM;
                System.Runtime.InteropServices.Marshal.StructureToPtr(wp, lParam, false);
            }
        }
        else if (msg == Nativo.WM_MOUSEACTIVATE)
        {
            tratado = true;
            return new IntPtr(Nativo.MA_NOACTIVATE);
        }
        else if (msg == Nativo.WM_DISPLAYCHANGE || msg == Nativo.WM_DPICHANGED)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background, Posicionar);
        }
        else if (_msgBarraCriada != 0 && msg == _msgBarraCriada)
        {
            // o Explorer reiniciou: a janela da área de trabalho é outra
            Dispatcher.BeginInvoke(DispatcherPriority.Background, PrenderNaAreaDeTrabalho);
        }
        return IntPtr.Zero;
    }

    // Centro horizontal do monitor principal; na vertical, o centro do relógio fica em
    // cfg.Altura da tela (0.40 = um pouco acima do meio).
    public void Posicionar()
    {
        if (_hwnd == IntPtr.Zero) return;
        var tela = System.Windows.Forms.Screen.PrimaryScreen;
        if (tela == null) return;
        var b = tela.Bounds; // em pixels de verdade (o processo é per-monitor)
        var dpi = VisualTreeHelper.GetDpi(this);
        int w = (int)Math.Round(ActualWidth * dpi.DpiScaleX);
        int h = (int)Math.Round(ActualHeight * dpi.DpiScaleY);
        if (w <= 0 || h <= 0) return;
        int x = b.X + (b.Width - w) / 2;
        int y = (int)Math.Round(b.Y + b.Height * _cfg.Altura - h / 2.0);
        Nativo.SetWindowPos(_hwnd, Nativo.HWND_BOTTOM, x, y, 0, 0, Nativo.SWP_NOSIZE | Nativo.SWP_NOACTIVATE);
    }

    // Para a medição: onde a janela está de verdade e se ficou presa na área de trabalho.
    public string Diagnostico()
    {
        Nativo.GetWindowRect(_hwnd, out var r);
        var dono = Nativo.GetWindowLongPtr(_hwnd, Nativo.GWLP_HWNDPARENT);
        var tela = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
        return $"janela={r.Left},{r.Top} {r.Right - r.Left}x{r.Bottom - r.Top} tela={tela.Width}x{tela.Height} " +
               $"centro_y={(r.Top + r.Bottom) / 2.0 / tela.Height:0.000} dono={Nativo.Classe(dono)} dpi={VisualTreeHelper.GetDpi(this).DpiScaleX}";
    }

    void Tique()
    {
        var agora = DateTime.Now;
        if (_cfg.Clima && agora - _ultimoClima > TimeSpan.FromMinutes(1))
        {
            _ultimoClima = agora;
            _ = AtualizarClima();
        }
        _m.AtualizarRelogio(agora, _cfg.Formato24h, _cfg.Clima ? _clima.Texto() : "", _cfg.Clima);

        if (agora - _ultimaMidia > TimeSpan.FromMilliseconds(480) && !_atualizandoMidia)
        {
            _ultimaMidia = agora;
            _ = AtualizarMidia();
        }
        _estado = ModoDemo ? Demo.Estado() : _midia.Estado();
        _m.AtualizarMidia(_estado);
        AjustarBarras();
        AcordarBarrasSeTiverSom();
        ConferirBarras();
    }

    long _tickQuadro, _tickReabrir;
    double _esperaReabrir = 6;

    // Rede de segurança: barras que deveriam estar vivas e não estão.
    void ConferirBarras()
    {
        if (!_barrasLigadas || ModoDemo) return;
        long agora = Environment.TickCount64;

        // o WPF parou de chamar o desenho (monitor que dormiu, tela bloqueada, driver de vídeo): inscreve de novo
        if (!_dormindo && Medidor == null && agora - _tickQuadro > 2500)
        {
            Log.Escrever($"barras: o WPF ficou {(agora - _tickQuadro) / 1000.0:0.0} s sem desenhar, inscrevendo de novo");
            CompositionTarget.Rendering -= Quadro;
            _ultimoQuadro = TimeSpan.Zero;
            _tickQuadro = agora;
            CompositionTarget.Rendering += Quadro;
        }

        // a música toca e nenhum som chega: a captura pode ter morrido sem avisar. Abre de novo, e se
        // era silêncio de verdade (nada chega mesmo) espaça as tentativas: 6 s, 12 s, ... no máximo 2 min
        double semSom = _audio.SegundosSemDados;
        if (semSom < 2) { _esperaReabrir = 6; return; }
        if (_estado.Tocando && semSom > 4 && agora - _tickReabrir > _esperaReabrir * 1000)
        {
            Log.Escrever($"barras: música tocando e nenhum som há {Math.Min(semSom, 999):0} s, reabrindo a captura");
            _tickReabrir = agora;
            _esperaReabrir = Math.Min(_esperaReabrir * 2, 120);
            _audio.Reabrir();
        }
    }

    async System.Threading.Tasks.Task AtualizarClima()
    {
        if (await _clima.Atualizar(_cfg.Latitude, _cfg.Longitude))
            _m.AtualizarRelogio(DateTime.Now, _cfg.Formato24h, _clima.Texto(), true);
    }

    async System.Threading.Tasks.Task AtualizarMidia()
    {
        _atualizandoMidia = true;
        try { await _midia.Atualizar(); }
        finally { _atualizandoMidia = false; }
    }

    void AjustarBarras()
    {
        bool querer = _estado.Tem && !_pausado;
        if (querer && !_barrasLigadas)
        {
            _barrasLigadas = true;
            _dormindo = false;
            _parado = 0;
            if (!ModoDemo) _audio.Ligar();
            _ultimoQuadro = TimeSpan.Zero;
            _tickQuadro = Environment.TickCount64;
            CompositionTarget.Rendering += Quadro;
        }
        else if (!querer && _barrasLigadas)
        {
            DesligarBarras();
        }
    }

    void DesligarBarras()
    {
        if (!_barrasLigadas) return;
        _barrasLigadas = false;
        if (!_dormindo) CompositionTarget.Rendering -= Quadro;
        _dormindo = false;
        _audio.Desligar();
        Array.Clear(_m.Barras.Entrada);
        _m.Barras.Passo(10);
        _m.Barras.InvalidateVisual();
    }

    void Quadro(object? s, EventArgs e)
    {
        var t = ((RenderingEventArgs)e).RenderingTime;
        _tickQuadro = Environment.TickCount64;
        if (t == _ultimoQuadro) return; // o WPF às vezes chama duas vezes no mesmo quadro
        double dt = _ultimoQuadro == TimeSpan.Zero ? 1 / 60.0 : (t - _ultimoQuadro).TotalSeconds;
        _ultimoQuadro = t;
        Medidor?.Quadro(t);

        if (ModoDemo)
        {
            int novas = Math.Clamp((int)(48000 * dt), 1, 4096);
            _audio.Alimentar(Demo.Amostras(t.TotalSeconds - dt, novas, 48000), 48000);
            _audio.Espectro(_m.Barras.Entrada);
        }
        else
        {
            _audio.Garantir();
            _audio.Espectro(_m.Barras.Entrada);
        }
        if (_m.Barras.Passo(dt))
        {
            _m.Barras.InvalidateVisual();
            _parado = 0;
        }
        else if ((_parado += dt) > 1.5 && Medidor == null)
        {
            // silêncio com as barras já no chão: para de pedir quadro ao WPF até voltar som
            _dormindo = true;
            CompositionTarget.Rendering -= Quadro;
        }
    }

    double _parado;
    bool _dormindo;

    void AcordarBarrasSeTiverSom()
    {
        if (!_barrasLigadas || !_dormindo) return;
        _audio.Garantir();
        if (_audio.Nivel() < 0.0001) return;
        _dormindo = false;
        _parado = 0;
        _ultimoQuadro = TimeSpan.Zero;
        _tickQuadro = Environment.TickCount64;
        CompositionTarget.Rendering += Quadro;
    }

    void VerMouse()
    {
        if (_hwnd == IntPtr.Zero || !Nativo.GetCursorPos(out var p)) return;
        bool emCima = false;
        var area = _m.AreaDaCapa();
        if (!area.IsEmpty && _m.Opacity > 0)
        {
            try { emCima = area.Contains(_m.PointFromScreen(new Point(p.X, p.Y))); } catch { }
        }
        // só a capa recebe clique; o resto continua atravessando para a área de trabalho
        if (emCima == _transparenteAoClique)
        {
            _transparenteAoClique = !emCima;
            var ex = (long)Nativo.GetWindowLongPtr(_hwnd, Nativo.GWL_EXSTYLE);
            ex = emCima ? ex & ~Nativo.WS_EX_TRANSPARENT : ex | Nativo.WS_EX_TRANSPARENT;
            Nativo.SetWindowLongPtr(_hwnd, Nativo.GWL_EXSTYLE, new IntPtr(ex));
        }
        _m.MostrarControles(emCima);
    }

    // Para as barras quando ninguém consegue vê-las: jogo ou vídeo em tela cheia, ou uma
    // janela em primeiro plano cobrindo o relógio inteiro.
    void Vigiar()
    {
        bool pausar = false;
        string motivo = "";
        if (_cfg.PausarEmTelaCheia)
        {
            if (Nativo.TelaCheiaOcupada(_hwnd)) { pausar = true; motivo = "tela cheia no monitor do relógio"; }
            else
            {
                var fg = Nativo.GetForegroundWindow();
                if (fg != IntPtr.Zero && fg != _hwnd && !Nativo.EhAreaDeTrabalho(fg) && Nativo.IsWindowVisible(fg)
                    && !Nativo.IsIconic(fg) && !Nativo.Encoberta(fg)
                    && Nativo.GetWindowRect(fg, out var r) && Nativo.GetWindowRect(_hwnd, out var eu))
                {
                    pausar = r.Left <= eu.Left && r.Top <= eu.Top && r.Right >= eu.Right && r.Bottom >= eu.Bottom;
                    if (pausar) motivo = "janela em primeiro plano cobrindo o relógio";
                }
            }
        }
        if (pausar != _pausado)
        {
            // fica no log para o caso de as barras "pararem": se foi isto, o motivo aparece aqui
            Log.Escrever(pausar ? "barras pausadas: " + motivo : "barras liberadas");
            _pausado = pausar;
            AjustarBarras();
        }
    }
}

// Conta quadros desenhados por segundo, para a regra dos 60 fps.
public sealed class Medidor
{
    readonly System.Collections.Generic.List<int> _porSegundo = new();
    int _contagem;
    TimeSpan _inicioSegundo = TimeSpan.Zero;
    public System.Collections.Generic.IReadOnlyList<int> PorSegundo => _porSegundo;
    public readonly Stopwatch Relogio = Stopwatch.StartNew();

    public void Quadro(TimeSpan t)
    {
        if (_inicioSegundo == TimeSpan.Zero) _inicioSegundo = t;
        _contagem++;
        if (t - _inicioSegundo >= TimeSpan.FromSeconds(1))
        {
            _porSegundo.Add(_contagem);
            _contagem = 0;
            _inicioSegundo = t;
        }
    }
}
