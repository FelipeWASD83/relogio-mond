using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace RelogioMond;

// Atualização pelas releases do GitHub, no esquema do Clave: o app procura, baixa,
// confere o SHA-256 e deixa o pacote pronto em dados/atualizacao; depois fecha com o
// código 3 e o iniciar.pyw troca os arquivos, reabre e volta para a versão anterior
// se a nova não abrir. dados/ e fontes/ (a Anurati) nunca são tocados.
public static class Atualizador
{
    public const int CodigoAplicar = 3;

    public sealed record Info(string Atual, string? Ultima, bool Nova, string Notas, string? UrlPacote, string? Digest, string? Erro);

    static readonly HttpClient Http = CriarHttp();

    static HttpClient CriarHttp()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd("RelogioMond/" + VersaoAtual());
        return h;
    }

    public static string VersaoAtual() => LerVersao(Path.Combine(Caminhos.Raiz, "versao.json")).versao;
    static string Repositorio() => LerVersao(Path.Combine(Caminhos.Raiz, "versao.json")).repo;

    static (string versao, string repo) LerVersao(string arq)
    {
        try
        {
            using var d = JsonDocument.Parse(File.ReadAllText(arq));
            return (d.RootElement.GetProperty("versao").GetString() ?? "0.0.0",
                    d.RootElement.TryGetProperty("repositorio", out var r) ? r.GetString() ?? "" : "");
        }
        catch { return ("0.0.0", ""); }
    }

    public static bool CopiaDeDesenvolvimento => Directory.Exists(Path.Combine(Caminhos.Raiz, ".git"));
    public static bool TemLancador => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RELOGIO_LANCADOR"));

    static Version Tupla(string v)
    {
        var partes = (v ?? "0").TrimStart('v', 'V').Split('.')
            .Select(p => int.TryParse(new string(p.TakeWhile(char.IsDigit).ToArray()), out var n) ? n : 0)
            .Concat(new[] { 0, 0, 0 }).Take(3).ToArray();
        return new Version(partes[0], partes[1], partes[2]);
    }

    public static async Task<Info> Checar()
    {
        var atual = VersaoAtual();
        var repo = Repositorio();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repo}/releases/latest");
            req.Headers.Accept.ParseAdd("application/vnd.github+json");
            req.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            using var resp = await Http.SendAsync(req);
            if (resp.StatusCode == HttpStatusCode.NotFound) return new Info(atual, null, false, "", null, null, Idioma.T("upd_sem_release"));
            resp.EnsureSuccessStatusCode();
            using var d = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var rel = d.RootElement;
            var ultima = (rel.GetProperty("tag_name").GetString() ?? "").TrimStart('v', 'V');
            string? url = null, digest = null;
            foreach (var a in rel.GetProperty("assets").EnumerateArray())
            {
                if ((a.GetProperty("name").GetString() ?? "").EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    url = a.GetProperty("browser_download_url").GetString();
                    digest = a.TryGetProperty("digest", out var dg) ? dg.GetString() : null;
                    break;
                }
            }
            var notas = rel.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
            return new Info(atual, ultima, Tupla(ultima) > Tupla(atual), notas, url, digest, null);
        }
        catch (Exception e)
        {
            return new Info(atual, null, false, "", null, null, e.Message);
        }
    }

    // Baixa, confere e deixa pronto. Devolve null se deu certo, ou o motivo.
    public static async Task<string?> BaixarEPreparar(Info info)
    {
        if (CopiaDeDesenvolvimento) return Idioma.T("upd_dev");
        if (!TemLancador) return Idioma.T("upd_sem_lancador");
        if (info.UrlPacote == null || info.Ultima == null) return Idioma.T("upd_pacote");
        // o SHA-256 que o GitHub calcula para cada arquivo da release
        if (info.Digest == null || !info.Digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) return Idioma.T("upd_hash");

        var pasta = Caminhos.Atualizacao;
        var zip = Path.Combine(pasta, "pacote.zip");
        var novo = Path.Combine(pasta, "novo");
        Directory.CreateDirectory(pasta);
        if (Directory.Exists(novo)) Directory.Delete(novo, true);

        using (var resp = await Http.GetAsync(info.UrlPacote, HttpCompletionOption.ResponseHeadersRead))
        {
            resp.EnsureSuccessStatusCode();
            await using var f = File.Create(zip);
            await resp.Content.CopyToAsync(f);
        }

        string hash;
        await using (var f = File.OpenRead(zip)) hash = Convert.ToHexString(await SHA256.HashDataAsync(f));
        if (!hash.Equals(info.Digest[7..], StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(zip);
            return Idioma.T("upd_hash");
        }

        var raizNovo = Path.GetFullPath(novo) + Path.DirectorySeparatorChar;
        using (var z = ZipFile.OpenRead(zip))
        {
            foreach (var e in z.Entries)
            {
                var alvo = Path.GetFullPath(Path.Combine(novo, e.FullName));
                if (!alvo.StartsWith(raizNovo, StringComparison.OrdinalIgnoreCase)) return Idioma.T("upd_pacote");
            }
            z.ExtractToDirectory(novo);
        }
        File.Delete(zip);

        var raiz = File.Exists(Path.Combine(novo, "versao.json")) ? novo
            : Directory.GetDirectories(novo).FirstOrDefault(d => File.Exists(Path.Combine(d, "versao.json")));
        if (raiz == null || !File.Exists(Path.Combine(raiz, "app", "RelogioMond.dll")) || !File.Exists(Path.Combine(raiz, "iniciar.pyw")))
            return Idioma.T("upd_pacote");
        if (Tupla(LerVersao(Path.Combine(raiz, "versao.json")).versao) != Tupla(info.Ultima))
            return Idioma.T("upd_pacote");

        File.WriteAllText(Path.Combine(pasta, "pronta.json"),
            JsonSerializer.Serialize(new { versao = info.Ultima, pasta = raiz }));
        return null;
    }

    // Recados que o iniciar.pyw deixa depois de aplicar (ou desfazer) uma atualização.
    public static string? RecadoDoLancador()
    {
        foreach (var (arq, chave) in new[] { ("aplicada.json", "upd_aplicada"), ("voltou.json", "upd_voltou") })
        {
            var p = Path.Combine(Caminhos.Atualizacao, arq);
            if (!File.Exists(p)) continue;
            try
            {
                using var d = JsonDocument.Parse(File.ReadAllText(p));
                var v = d.RootElement.GetProperty("versao").GetString() ?? "";
                File.Delete(p);
                return Idioma.T(chave, v);
            }
            catch { try { File.Delete(p); } catch { } }
        }
        return null;
    }
}
