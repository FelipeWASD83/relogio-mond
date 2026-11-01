using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using WF = System.Windows.Forms;

namespace RelogioMond;

// O ícone perto do relógio do Windows: é por ele que se mexe no relógio, já que a janela
// deixa o clique passar.
public sealed class Bandeja : IDisposable
{
    readonly WF.NotifyIcon _icone;
    readonly Func<Config> _cfg;
    readonly Action<Config> _salvar;
    readonly Action _recarregar, _procurarAtualizacao, _sair;

    public Bandeja(Func<Config> cfg, Action<Config> salvar, Action recarregar, Action procurarAtualizacao, Action sair)
    {
        _cfg = cfg;
        _salvar = salvar;
        _recarregar = recarregar;
        _procurarAtualizacao = procurarAtualizacao;
        _sair = sair;
        _icone = new WF.NotifyIcon { Icon = CarregarIcone(), Visible = true };
        _icone.ContextMenuStrip = new WF.ContextMenuStrip();
        _icone.ContextMenuStrip.Opening += (_, _) => Montar();
        Montar();
    }

    static Icon CarregarIcone()
    {
        try
        {
            var s = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/relogio-mond.ico"))?.Stream;
            if (s != null) return new Icon(s, 32, 32);
        }
        catch (Exception e) { Log.Escrever("ícone da bandeja: " + e.Message); }
        return SystemIcons.Application;
    }

    public void Montar()
    {
        var c = _cfg();
        _icone.Text = Idioma.T("versao", Atualizador.VersaoAtual());
        var m = _icone.ContextMenuStrip!;
        m.Items.Clear();
        m.Items.Add(new WF.ToolStripMenuItem(Idioma.T("versao", Atualizador.VersaoAtual())) { Enabled = false });
        m.Items.Add(new WF.ToolStripSeparator());

        var idioma = new WF.ToolStripMenuItem(Idioma.T("idioma"));
        foreach (var (cod, nome) in new[] { ("auto", Idioma.T("auto")), ("pt", "Português"), ("en", "English") })
        {
            var item = new WF.ToolStripMenuItem(nome) { Checked = c.Idioma == cod };
            item.Click += (_, _) => Mudar(x => x.Idioma = cod);
            idioma.DropDownItems.Add(item);
        }
        m.Items.Add(idioma);
        m.Items.Add(Opcao(Idioma.T("clima"), c.Clima, x => x.Clima = !x.Clima));
        m.Items.Add(Opcao(Idioma.T("24h"), c.Formato24h, x => x.Formato24h = !x.Formato24h));
        m.Items.Add(Opcao(Idioma.T("iniciar"), c.IniciarComWindows, x => x.IniciarComWindows = !x.IniciarComWindows));
        m.Items.Add(new WF.ToolStripSeparator());
        m.Items.Add(Acao(Idioma.T("config"), () =>
        {
            try { Process.Start(new ProcessStartInfo("notepad.exe", $"\"{Config.Arquivo}\"") { UseShellExecute = true }); }
            catch (Exception e) { Log.Escrever("abrir config: " + e.Message); }
        }));
        m.Items.Add(Acao(Idioma.T("recarregar"), _recarregar));
        m.Items.Add(Acao(Idioma.T("atualizar"), _procurarAtualizacao));
        m.Items.Add(new WF.ToolStripSeparator());
        m.Items.Add(Acao(Idioma.T("sair"), _sair));
    }

    WF.ToolStripMenuItem Opcao(string texto, bool marcado, Action<Config> mudar)
    {
        var i = new WF.ToolStripMenuItem(texto) { Checked = marcado };
        i.Click += (_, _) => Mudar(mudar);
        return i;
    }

    static WF.ToolStripMenuItem Acao(string texto, Action acao)
    {
        var i = new WF.ToolStripMenuItem(texto);
        i.Click += (_, _) => acao();
        return i;
    }

    void Mudar(Action<Config> mudar)
    {
        var c = _cfg();
        mudar(c);
        _salvar(c);
    }

    public void Avisar(string texto)
    {
        _icone.BalloonTipTitle = Idioma.T("versao", Atualizador.VersaoAtual());
        _icone.BalloonTipText = texto;
        _icone.ShowBalloonTip(6000);
    }

    public void Dispose()
    {
        _icone.Visible = false;
        _icone.Dispose();
    }
}
