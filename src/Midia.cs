using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace RelogioMond;

public sealed class EstadoMidia
{
    public bool Tem { get; init; }
    public string Titulo { get; init; } = "";
    public string Artista { get; init; } = "";
    public BitmapSource? Capa { get; init; }
    public bool Tocando { get; init; }
    public TimeSpan Posicao { get; init; }
    public TimeSpan Duracao { get; init; }
}

// Lê o que está tocando pelo controle de mídia do Windows (o mesmo do pop-up de
// volume): Spotify, YouTube no navegador, players. Prefere a sessão que está tocando.
public sealed class Midia
{
    GlobalSystemMediaTransportControlsSessionManager? _gerente;
    GlobalSystemMediaTransportControlsSession? _sessao;
    string _chaveFaixa = "";
    string _titulo = "", _artista = "";
    BitmapSource? _capa;
    bool _lendoPropriedades;
    bool _propriedadesSujas = true;
    int _tentativasCapa;

    // posição informada pela sessão e quando, para andar o relógio entre uma notícia e outra
    TimeSpan _posBase, _duracao;
    DateTimeOffset _posQuando;
    bool _tocando;
    double _velocidade = 1;

    public async Task Iniciar()
    {
        try
        {
            _gerente = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _gerente.SessionsChanged += (_, _) => _propriedadesSujas = true;
            _gerente.CurrentSessionChanged += (_, _) => _propriedadesSujas = true;
        }
        catch (Exception e)
        {
            Log.Escrever("controle de mídia do Windows indisponível: " + e.Message);
        }
    }

    // Chamado umas duas vezes por segundo pela janela.
    public async Task Atualizar()
    {
        if (_gerente == null) return;
        GlobalSystemMediaTransportControlsSession? escolhida = null;
        try
        {
            var sessoes = _gerente.GetSessions();
            escolhida = sessoes.FirstOrDefault(s => Status(s) == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                        ?? (_sessao != null && sessoes.Any(s => s.SourceAppUserModelId == _sessao.SourceAppUserModelId) ? _sessao : null)
                        ?? _gerente.GetCurrentSession()
                        ?? sessoes.FirstOrDefault();
        }
        catch (Exception e)
        {
            Log.Escrever("lendo sessões de mídia: " + e.Message);
        }

        if (!ReferenceEquals(escolhida, _sessao))
        {
            if (_sessao != null) _sessao.MediaPropertiesChanged -= AoMudarPropriedades;
            _sessao = escolhida;
            if (_sessao != null) _sessao.MediaPropertiesChanged += AoMudarPropriedades;
            _propriedadesSujas = true;
            if (_sessao == null) { _titulo = _artista = ""; _capa = null; _chaveFaixa = ""; }
        }
        if (_sessao == null) return;

        try
        {
            var info = _sessao.GetPlaybackInfo();
            _tocando = info?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            _velocidade = info?.PlaybackRate ?? 1;
            if (_velocidade <= 0) _velocidade = 1;
            var tl = _sessao.GetTimelineProperties();
            if (tl != null)
            {
                _duracao = tl.EndTime - tl.StartTime;
                _posBase = tl.Position - tl.StartTime;
                _posQuando = tl.LastUpdatedTime;
            }
        }
        catch (Exception e)
        {
            Log.Escrever("lendo posição da mídia: " + e.Message);
        }

        if (_propriedadesSujas && !_lendoPropriedades) await LerPropriedades(_sessao);
    }

    void AoMudarPropriedades(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs a) => _propriedadesSujas = true;

    async Task LerPropriedades(GlobalSystemMediaTransportControlsSession s)
    {
        _lendoPropriedades = true;
        _propriedadesSujas = false;
        try
        {
            var p = await s.TryGetMediaPropertiesAsync();
            if (p == null) return;
            var chave = p.Title + "\u0001" + p.Artist + "\u0001" + p.AlbumTitle;
            _titulo = p.Title ?? "";
            _artista = string.IsNullOrWhiteSpace(p.Artist) ? p.AlbumArtist ?? "" : p.Artist;
            // a capa costuma chegar um pouco depois do título: tenta de novo enquanto não vier
            if (chave != _chaveFaixa) { _chaveFaixa = chave; _tentativasCapa = 0; _capa = null; }
            if (_capa == null && _tentativasCapa < 8)
            {
                _tentativasCapa++;
                _capa = p.Thumbnail != null ? await LerCapa(p.Thumbnail) : null;
                if (_capa == null && !string.IsNullOrEmpty(_titulo)) _propriedadesSujas = true;
            }
        }
        catch (Exception e)
        {
            Log.Escrever($"lendo título e capa de {s.SourceAppUserModelId}: {e.GetType().Name} 0x{e.HResult:X8} {e.Message}");
        }
        finally
        {
            _lendoPropriedades = false;
        }
    }

    static async Task<BitmapSource?> LerCapa(IRandomAccessStreamReference referencia)
    {
        try
        {
            using IRandomAccessStreamWithContentType fluxo = await referencia.OpenReadAsync();
            if (fluxo.Size == 0) return null;
            using var ms = new MemoryStream();
            await fluxo.AsStreamForRead().CopyToAsync(ms);
            ms.Position = 0;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = 300;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return Recortar(bmp);
        }
        catch (Exception e)
        {
            Log.Escrever("capa ilegível: " + e.Message);
            return null;
        }
    }

    // Capa de vídeo do YouTube chega 16:9; o quadrado do relógio pega o meio.
    static BitmapSource Recortar(BitmapSource b)
    {
        int w = b.PixelWidth, h = b.PixelHeight;
        if (Math.Abs(w - h) <= 2) return b;
        int lado = Math.Min(w, h);
        var c = new CroppedBitmap(b, new System.Windows.Int32Rect((w - lado) / 2, (h - lado) / 2, lado, lado));
        c.Freeze();
        return c;
    }

    public EstadoMidia Estado()
    {
        if (_sessao == null || string.IsNullOrWhiteSpace(_titulo)) return new EstadoMidia();
        var pos = _posBase;
        if (_tocando && _posQuando != default)
        {
            var passou = DateTimeOffset.Now - _posQuando;
            if (passou > TimeSpan.Zero && passou < TimeSpan.FromHours(6))
                pos += TimeSpan.FromTicks((long)(passou.Ticks * _velocidade));
        }
        if (_duracao > TimeSpan.Zero && pos > _duracao) pos = _duracao;
        if (pos < TimeSpan.Zero) pos = TimeSpan.Zero;
        return new EstadoMidia
        {
            Tem = true,
            Titulo = _titulo,
            Artista = _artista,
            Capa = _capa,
            Tocando = _tocando,
            Posicao = pos,
            Duracao = _duracao,
        };
    }

    public async void Anterior() { try { if (_sessao != null) await _sessao.TrySkipPreviousAsync(); } catch (Exception e) { Log.Escrever("anterior: " + e.Message); } }
    public async void TocarPausar() { try { if (_sessao != null) await _sessao.TryTogglePlayPauseAsync(); } catch (Exception e) { Log.Escrever("tocar/pausar: " + e.Message); } }
    public async void Proxima() { try { if (_sessao != null) await _sessao.TrySkipNextAsync(); } catch (Exception e) { Log.Escrever("próxima: " + e.Message); } }

    static GlobalSystemMediaTransportControlsSessionPlaybackStatus? Status(GlobalSystemMediaTransportControlsSession s)
    {
        try { return s.GetPlaybackInfo()?.PlaybackStatus; } catch { return null; }
    }
}
