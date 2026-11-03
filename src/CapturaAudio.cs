using System;
using System.Numerics;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace RelogioMond;

// Escuta o que está saindo da saída de som padrão (loopback do WASAPI) e entrega o
// espectro em faixas logarítmicas para o visualizador. Troca sozinha quando a saída
// padrão muda (fone, caixa, HDMI da TV).
public sealed class CapturaAudio : IDisposable
{
    const int TamanhoFft = 4096;
    readonly float[] _anel = new float[TamanhoFft * 2];
    int _escrita;
    long _ultimaAmostra, _abertaEm;
    readonly object _trava = new();

    WasapiLoopbackCapture? _captura;
    string? _dispositivo;
    public int Aberturas { get; private set; }
    int _taxa = 48000;
    DateTime _proximaChecagem;
    bool _ligada;

    readonly Complex[] _fft = new Complex[TamanhoFft];
    readonly float[] _janela = new float[TamanhoFft];
    readonly float[] _amostras = new float[TamanhoFft];

    public CapturaAudio()
    {
        for (int i = 0; i < TamanhoFft; i++)
            _janela[i] = (float)(0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (TamanhoFft - 1)));
    }

    public void Ligar()
    {
        _ligada = true;
        Garantir(true);
    }

    public void Desligar()
    {
        _ligada = false;
        Fechar();
    }

    // Chamado a cada quadro: confere de vez em quando se a saída padrão mudou.
    public void Garantir(bool forcar = false)
    {
        if (!_ligada) return;
        if (!forcar && DateTime.UtcNow < _proximaChecagem) return;
        _proximaChecagem = DateTime.UtcNow.AddSeconds(2);
        try
        {
            using var en = new MMDeviceEnumerator();
            using var dev = en.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            if (_captura != null && dev.ID == _dispositivo) return;
            Fechar();
            _dispositivo = dev.ID;
            var cap = new WasapiLoopbackCapture(en.GetDevice(dev.ID));
            _taxa = cap.WaveFormat.SampleRate;
            cap.DataAvailable += AoChegar;
            // O NAudio avisa "parou" de forma assíncrona, e o aviso da captura ANTIGA chega depois da nova já
            // aberta. Só vale o aviso da captura que está em uso; senão ele apagava o dispositivo da nova,
            // a checagem seguinte achava que a saída tinha mudado e a captura se reabria a cada 2 s.
            cap.RecordingStopped += (_, e) =>
            {
                if (!ReferenceEquals(cap, _captura)) return;
                if (e.Exception != null) Log.Escrever("captura de áudio parou: " + e.Exception.Message);
                _dispositivo = null;
            };
            cap.StartRecording();
            _captura = cap;
            _abertaEm = Environment.TickCount64;
            Aberturas++;
        }
        catch (Exception e)
        {
            Log.Escrever("sem captura de áudio: " + e.Message);
            Fechar();
            _proximaChecagem = DateTime.UtcNow.AddSeconds(5);
        }
    }

    // Fecha e abre de novo a captura. Usado quando a música toca e nenhum som chega há tempo demais:
    // a captura pode ter morrido sem avisar (troca de formato da placa, driver, saída que voltou).
    public void Reabrir()
    {
        if (!_ligada) return;
        Fechar();
        Garantir(true);
    }

    // Há quanto tempo não chega nenhuma amostra. A captura loopback não entrega nada enquanto o
    // Windows não toca som, então um valor alto é silêncio de verdade ou captura morta.
    public double SegundosSemDados
    {
        get
        {
            lock (_trava)
            {
                // conta desde a última amostra ou desde que a captura abriu, o que for mais recente
                long desde = Math.Max(_ultimaAmostra, _abertaEm);
                return desde == 0 ? double.MaxValue : (Environment.TickCount64 - desde) / 1000.0;
            }
        }
    }

    void Fechar()
    {
        var c = _captura;
        _captura = null;
        _dispositivo = null;
        if (c == null) return;
        try { c.DataAvailable -= AoChegar; c.StopRecording(); } catch { }
        try { c.Dispose(); } catch { }
    }

    void AoChegar(object? s, WaveInEventArgs e)
    {
        var cap = _captura;
        if (cap == null) return;
        var wf = cap.WaveFormat;
        int canais = wf.Channels;
        int bytesAmostra = wf.BitsPerSample / 8;
        int quadros = e.BytesRecorded / (bytesAmostra * canais);
        // o formato de mixagem do Windows em modo compartilhado é float de 32 bits na prática
        bool flutuante = wf.Encoding == WaveFormatEncoding.IeeeFloat ||
                         (wf.Encoding == WaveFormatEncoding.Extensible && wf.BitsPerSample == 32);
        lock (_trava)
        {
            for (int q = 0; q < quadros; q++)
            {
                float soma = 0;
                int baseByte = q * bytesAmostra * canais;
                for (int c = 0; c < canais; c++)
                {
                    int o = baseByte + c * bytesAmostra;
                    soma += flutuante && bytesAmostra == 4
                        ? BitConverter.ToSingle(e.Buffer, o)
                        : bytesAmostra == 2 ? BitConverter.ToInt16(e.Buffer, o) / 32768f
                        : bytesAmostra == 4 ? BitConverter.ToInt32(e.Buffer, o) / 2147483648f : 0f;
                }
                soma /= canais;
                // um NaN ou infinito vindo do driver contaminaria a FFT e o ganho automático para sempre
                _anel[_escrita] = float.IsFinite(soma) ? soma : 0f;
                _escrita = (_escrita + 1) % _anel.Length;
            }
            if (quadros > 0) _ultimaAmostra = Environment.TickCount64;
        }
    }

    // Para o teste: entra com amostras como se tivessem vindo da placa de som.
    public void Alimentar(float[] amostras, int taxa)
    {
        lock (_trava)
        {
            _taxa = taxa;
            foreach (var a in amostras) { _anel[_escrita] = float.IsFinite(a) ? a : 0f; _escrita = (_escrita + 1) % _anel.Length; }
            _ultimaAmostra = Environment.TickCount64;
        }
    }

    // Volume eficaz dos últimos ~20 ms (0 em silêncio).
    public float Nivel()
    {
        lock (_trava)
        {
            if (Environment.TickCount64 - _ultimaAmostra > 250) return 0;
            double soma = 0;
            for (int i = 1; i <= 1024; i++)
            {
                var v = _anel[(_escrita - i + _anel.Length) % _anel.Length];
                soma += v * v;
            }
            return (float)Math.Sqrt(soma / 1024);
        }
    }

    // Preenche `saida` (0..1) com a energia de cada faixa. Devolve false se está em silêncio.
    public bool Espectro(float[] saida)
    {
        bool viva;
        lock (_trava)
        {
            viva = Environment.TickCount64 - _ultimaAmostra < 250;
            int ini = (_escrita - TamanhoFft + _anel.Length) % _anel.Length;
            for (int i = 0; i < TamanhoFft; i++) _amostras[i] = _anel[(ini + i) % _anel.Length];
        }
        if (!viva)
        {
            Array.Clear(saida);
            return false;
        }
        for (int i = 0; i < TamanhoFft; i++) _fft[i] = new Complex(_amostras[i] * _janela[i], 0);
        Fft(_fft);

        int n = saida.Length;
        double fMin = 40, fMax = Math.Min(16000, _taxa / 2.0 - 100);
        double binHz = (double)_taxa / TamanhoFft;
        for (int b = 0; b < n; b++)
        {
            double f0 = fMin * Math.Pow(fMax / fMin, (double)b / n);
            double f1 = fMin * Math.Pow(fMax / fMin, (double)(b + 1) / n);
            int i0 = Math.Max(1, (int)Math.Floor(f0 / binHz));
            int i1 = Math.Max(i0, (int)Math.Ceiling(f1 / binHz));
            double pico = 0;
            for (int i = i0; i <= i1 && i < TamanhoFft / 2; i++) pico = Math.Max(pico, _fft[i].Magnitude);
            // magnitude normalizada pela janela de Hann (ganho 0.5) e pelo tamanho
            double amp = pico / (TamanhoFft * 0.25);
            double db = 20 * Math.Log10(amp + 1e-9);
            // inclinação de +4,5 dB por oitava: música perde energia nos agudos e as barras da direita morreriam
            double oitavas = Math.Log2(Math.Sqrt(f0 * f1) / 100.0);
            db += 4.5 * oitavas;
            saida[b] = (float)Math.Clamp((db + 56) / 46, 0, 1);
        }

        // Ganho automático: a faixa mais forte dos últimos segundos vai ao topo, então música baixa
        // (ou volume do Windows baixo) enche as barras como a alta. O pico lembrado cai com meia-vida
        // de ~4 s; o piso de 0,3 impede de aumentar ruído de fundo.
        long agora = Environment.TickCount64;
        double dt = _ultimaChamada == 0 ? 0 : Math.Min(1.0, (agora - _ultimaChamada) / 1000.0);
        _ultimaChamada = agora;
        float maior = 0;
        for (int b = 0; b < n; b++)
        {
            if (!float.IsFinite(saida[b])) saida[b] = 0;
            maior = Math.Max(maior, saida[b]);
        }
        if (!float.IsFinite(_picoLembrado)) _picoLembrado = 0;
        _picoLembrado = Math.Max(maior, _picoLembrado * (float)Math.Exp(-dt / 6.0));
        float ganho = 1f / Math.Max(_picoLembrado, 0.3f);
        for (int b = 0; b < n; b++)
        {
            // a curva (>1) separa barra alta de baixa: mais contraste, mais "pulso"
            saida[b] = (float)Math.Pow(Math.Clamp(saida[b] * ganho, 0f, 1f), 1.1);
        }
        return true;
    }

    float _picoLembrado;
    long _ultimaChamada;

    static void Fft(Complex[] a)
    {
        int n = a.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) (a[i], a[j]) = (a[j], a[i]);
        }
        for (int len = 2; len <= n; len <<= 1)
        {
            double ang = -2 * Math.PI / len;
            var wl = new Complex(Math.Cos(ang), Math.Sin(ang));
            for (int i = 0; i < n; i += len)
            {
                var w = Complex.One;
                for (int k = 0; k < len / 2; k++)
                {
                    var u = a[i + k];
                    var v = a[i + k + len / 2] * w;
                    a[i + k] = u + v;
                    a[i + k + len / 2] = u - v;
                    w *= wl;
                }
            }
        }
    }

    public void Dispose() => Fechar();
}
