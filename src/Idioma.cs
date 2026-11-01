using System;
using System.Collections.Generic;
using System.Globalization;

namespace RelogioMond;

public static class Idioma
{
    public static bool Ingles { get; private set; }

    public static void Definir(string escolha)
    {
        Ingles = escolha switch
        {
            "en" => true,
            "pt" => false,
            _ => !CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("pt", StringComparison.OrdinalIgnoreCase),
        };
    }

    static readonly string[] DiasPt = { "DOMINGO", "SEGUNDA", "TERÇA", "QUARTA", "QUINTA", "SEXTA", "SÁBADO" };
    static readonly string[] DiasEn = { "SUNDAY", "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY" };
    static readonly string[] MesesPt = { "JANEIRO", "FEVEREIRO", "MARÇO", "ABRIL", "MAIO", "JUNHO", "JULHO", "AGOSTO", "SETEMBRO", "OUTUBRO", "NOVEMBRO", "DEZEMBRO" };
    static readonly string[] MesesEn = { "JANUARY", "FEBRUARY", "MARCH", "APRIL", "MAY", "JUNE", "JULY", "AUGUST", "SEPTEMBER", "OCTOBER", "NOVEMBER", "DECEMBER" };

    public static string Dia(DateTime d) => (Ingles ? DiasEn : DiasPt)[(int)d.DayOfWeek];

    public static string Data(DateTime d) => Ingles
        ? $"{MesesEn[d.Month - 1]} {d.Day}, {d.Year}"
        : $"{d.Day} DE {MesesPt[d.Month - 1]}, {d.Year}";

    public static string Hora(DateTime d, bool h24) => h24
        ? d.ToString("HH:mm", CultureInfo.InvariantCulture)
        : d.ToString("h:mm", CultureInfo.InvariantCulture) + (d.Hour < 12 ? " AM" : " PM");

    static readonly Dictionary<string, (string Pt, string En)> Textos = new()
    {
        ["idioma"] = ("Idioma", "Language"),
        ["auto"] = ("Automático (Windows)", "Automatic (Windows)"),
        ["clima"] = ("Mostrar o clima", "Show the weather"),
        ["24h"] = ("Relógio de 24 horas", "24-hour clock"),
        ["iniciar"] = ("Iniciar com o Windows", "Start with Windows"),
        ["config"] = ("Editar configurações (tamanho, posição...)", "Edit settings (size, position...)"),
        ["recarregar"] = ("Recarregar", "Reload"),
        ["atualizar"] = ("Procurar atualização", "Check for updates"),
        ["sair"] = ("Sair", "Quit"),
        ["versao"] = ("Relógio Mond {0}", "Mond Clock {0}"),
        ["sem_fonte"] = ("A fonte Anurati não está instalada: o dia aparece com a fonte de reserva. Rode ferramentas\\instalar.ps1.",
                         "The Anurati font is not installed: the day uses the fallback font. Run ferramentas\\instalar.ps1."),
        ["upd_titulo"] = ("Atualização", "Update"),
        ["upd_procurando"] = ("Procurando versão nova...", "Checking for a new version..."),
        ["upd_em_dia"] = ("Você já está na versão mais nova ({0}).", "You are on the newest version ({0})."),
        ["upd_nova"] = ("Versão {0} disponível (você tem a {1}).", "Version {0} is available (you have {1})."),
        ["upd_botao"] = ("Atualizar agora", "Update now"),
        ["upd_fechar"] = ("Fechar", "Close"),
        ["upd_baixando"] = ("Baixando e conferindo o pacote...", "Downloading and checking the package..."),
        ["upd_pronto"] = ("Pronto. O relógio vai reabrir na versão {0}.", "Done. The clock will reopen on version {0}."),
        ["upd_dev"] = ("Esta é a cópia de desenvolvimento (tem .git): atualize pelo git.", "This is the development copy (it has .git): update it with git."),
        ["upd_sem_lancador"] = ("Abra o relógio pelo atalho para poder atualizar.", "Open the clock from its shortcut to be able to update."),
        ["upd_erro"] = ("Não deu: {0}", "It failed: {0}"),
        ["upd_sem_release"] = ("O repositório ainda não tem versão publicada.", "The repository has no published version yet."),
        ["upd_hash"] = ("O pacote baixado não confere com o SHA-256 do GitHub.", "The downloaded package does not match GitHub's SHA-256."),
        ["upd_pacote"] = ("O pacote não parece ser do relógio.", "The package does not look like the clock."),
        ["upd_voltou"] = ("A versão {0} não abriu e o relógio voltou para a anterior.", "Version {0} did not open and the clock went back to the previous one."),
        ["upd_aplicada"] = ("Atualizado para a versão {0}.", "Updated to version {0}."),
        ["anterior"] = ("Anterior", "Previous"),
        ["tocar"] = ("Tocar ou pausar", "Play or pause"),
        ["proxima"] = ("Próxima", "Next"),
    };

    public static string T(string chave, params object[] args)
    {
        if (!Textos.TryGetValue(chave, out var t)) return chave;
        var s = Ingles ? t.En : t.Pt;
        return args.Length > 0 ? string.Format(CultureInfo.InvariantCulture, s, args) : s;
    }

    // Códigos WMO do Open-Meteo.
    public static string Tempo(int codigo) => codigo switch
    {
        0 => Ingles ? "CLEAR" : "CÉU LIMPO",
        1 => Ingles ? "MOSTLY CLEAR" : "POUCAS NUVENS",
        2 => Ingles ? "PARTLY CLOUDY" : "PARCIALMENTE NUBLADO",
        3 => Ingles ? "OVERCAST" : "NUBLADO",
        45 or 48 => Ingles ? "FOG" : "NEBLINA",
        51 or 53 or 55 or 56 or 57 => Ingles ? "DRIZZLE" : "GAROA",
        61 or 63 or 66 => Ingles ? "RAIN" : "CHUVA",
        65 or 67 => Ingles ? "HEAVY RAIN" : "CHUVA FORTE",
        71 or 73 or 75 or 77 or 85 or 86 => Ingles ? "SNOW" : "NEVE",
        80 or 81 => Ingles ? "SHOWERS" : "PANCADAS DE CHUVA",
        82 => Ingles ? "HEAVY SHOWERS" : "TEMPORAL",
        95 => Ingles ? "THUNDERSTORM" : "TROVOADA",
        96 or 99 => Ingles ? "HAILSTORM" : "TEMPESTADE DE GRANIZO",
        _ => "",
    };
}
