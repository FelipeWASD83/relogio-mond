using System;
using System.IO;
using Microsoft.Win32;

namespace RelogioMond;

// "Iniciar com o Windows": uma linha em HKCU\...\Run abrindo o iniciar.pyw pelo pythonw.
public static class InicioAutomatico
{
    const string Chave = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string Nome = "RelogioMond";

    static string? Comando()
    {
        var pythonw = Environment.GetEnvironmentVariable("RELOGIO_PYTHONW");
        if (string.IsNullOrEmpty(pythonw) || !File.Exists(pythonw))
        {
            foreach (var d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
            {
                var c = Path.Combine(d.Trim(), "pythonw.exe");
                if (d.Length > 0 && File.Exists(c) && !c.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase)) { pythonw = c; break; }
            }
        }
        var script = Path.Combine(Caminhos.Raiz, "iniciar.pyw");
        if (string.IsNullOrEmpty(pythonw) || !File.Exists(script)) return null;
        return $"\"{pythonw}\" \"{script}\"";
    }

    public static void Aplicar(bool ligado)
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(Chave);
            if (!ligado)
            {
                if (k.GetValue(Nome) != null) k.DeleteValue(Nome, false);
                return;
            }
            var cmd = Comando();
            if (cmd == null)
            {
                Log.Escrever("iniciar com o Windows: não achei o pythonw nem o iniciar.pyw");
                return;
            }
            if ((k.GetValue(Nome) as string) != cmd) k.SetValue(Nome, cmd);
        }
        catch (Exception e)
        {
            Log.Escrever("iniciar com o Windows: " + e.Message);
        }
    }
}
