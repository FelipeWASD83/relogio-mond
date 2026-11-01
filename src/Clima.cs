using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace RelogioMond;

// Temperatura e condição atuais pelo Open-Meteo (sem chave). Pergunta a cada 15 min.
public sealed class Clima
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    DateTime _proxima = DateTime.MinValue;
    double _lat, _lon;

    public double? Temperatura { get; private set; }
    public int Codigo { get; private set; } = -1;

    static Clima() => Http.DefaultRequestHeaders.UserAgent.ParseAdd("RelogioMond/1 (+https://github.com/FelipeWASD83/relogio-mond)");

    public async Task<bool> Atualizar(double lat, double lon)
    {
        if (lat != _lat || lon != _lon) { _lat = lat; _lon = lon; _proxima = DateTime.MinValue; }
        if (DateTime.UtcNow < _proxima) return false;
        _proxima = DateTime.UtcNow.AddMinutes(15);
        try
        {
            var url = string.Format(CultureInfo.InvariantCulture,
                "https://api.open-meteo.com/v1/forecast?latitude={0:0.####}&longitude={1:0.####}&current=temperature_2m,weather_code&timezone=auto",
                lat, lon);
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(url));
            var atual = doc.RootElement.GetProperty("current");
            Temperatura = atual.GetProperty("temperature_2m").GetDouble();
            Codigo = atual.GetProperty("weather_code").GetInt32();
            return true;
        }
        catch (Exception e)
        {
            Log.Escrever("clima: " + e.Message);
            _proxima = DateTime.UtcNow.AddMinutes(2);
            return false;
        }
    }

    public string Texto()
    {
        if (Temperatura == null) return "";
        var cond = Idioma.Tempo(Codigo);
        var t = Math.Round(Temperatura.Value).ToString(CultureInfo.InvariantCulture) + "°";
        return string.IsNullOrEmpty(cond) ? t : t + "  " + cond;
    }
}
