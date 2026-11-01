using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RelogioMond;

public sealed class Config
{
    // Tamanho: 1.0 é o tamanho pensado para o ultrawide 3440x1440 a 125%.
    public double Escala { get; set; } = 1.0;
    // Onde fica o centro do relógio, em fração da altura da tela (0.5 = meio exato).
    public double Altura { get; set; } = 0.425;
    // "auto" segue o idioma do Windows; "pt" ou "en" fixam.
    public string Idioma { get; set; } = "auto";
    public bool Formato24h { get; set; } = true;
    public int Barras { get; set; } = 56;
    public double Opacidade { get; set; } = 1.0;
    public bool Clima { get; set; } = true;
    public double Latitude { get; set; } = -19.92;
    public double Longitude { get; set; } = -43.94;
    public bool IniciarComWindows { get; set; } = true;
    public bool PausarEmTelaCheia { get; set; } = true;

    static readonly JsonSerializerOptions Opcoes = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static string Arquivo => Path.Combine(Caminhos.Dados, "config.json");

    public static Config Carregar()
    {
        try
        {
            if (File.Exists(Arquivo))
            {
                var c = JsonSerializer.Deserialize<Config>(File.ReadAllText(Arquivo), Opcoes);
                if (c != null) return c.Normalizada();
            }
        }
        catch (Exception e)
        {
            Log.Escrever("config.json ilegível, usando o padrão: " + e.Message);
            return new Config();
        }
        var nova = new Config();
        nova.Salvar();
        return nova;
    }

    Config Normalizada()
    {
        Escala = Math.Clamp(Escala, 0.4, 3.0);
        Altura = Math.Clamp(Altura, 0.15, 0.85);
        Barras = Math.Clamp(Barras, 8, 160);
        Opacidade = Math.Clamp(Opacidade, 0.2, 1.0);
        if (Idioma != "pt" && Idioma != "en") Idioma = "auto";
        return this;
    }

    public void Salvar()
    {
        try
        {
            Directory.CreateDirectory(Caminhos.Dados);
            File.WriteAllText(Arquivo, JsonSerializer.Serialize(this, Opcoes));
        }
        catch (Exception e)
        {
            Log.Escrever("não deu para salvar o config.json: " + e.Message);
        }
    }
}

public static class Caminhos
{
    public static readonly string Raiz = AcharRaiz();
    public static string Dados => Path.Combine(Raiz, "dados");
    public static string Fontes => Path.Combine(Raiz, "fontes");
    public static string FontesDoApp => Path.Combine(AppContext.BaseDirectory, "fontes");
    public static string Atualizacao => Path.Combine(Dados, "atualizacao");

    static string AcharRaiz()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null)
        {
            if (File.Exists(Path.Combine(d.FullName, "versao.json"))) return d.FullName;
            d = d.Parent;
        }
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
    }
}

public static class Log
{
    static readonly object Trava = new();

    public static void Escrever(string texto)
    {
        try
        {
            lock (Trava)
            {
                Directory.CreateDirectory(Caminhos.Dados);
                var arq = Path.Combine(Caminhos.Dados, "relogio.log");
                if (File.Exists(arq) && new FileInfo(arq).Length > 512 * 1024)
                    File.Move(arq, arq + ".1", true);
                File.AppendAllText(arq, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {texto}{Environment.NewLine}");
            }
        }
        catch { }
    }
}
