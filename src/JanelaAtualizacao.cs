using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RelogioMond;

// Janelinha de "Procurar atualização": diz se tem versão nova, mostra o que mudou e
// baixa quando ele manda.
public sealed class JanelaAtualizacao : Window
{
    readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, FontSize = 15, Margin = new Thickness(0, 0, 0, 12) };
    readonly TextBox _notas = new()
    {
        IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        Height = 220, FontSize = 13, BorderThickness = new Thickness(1), Padding = new Thickness(8),
        Background = new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF2)), Visibility = Visibility.Collapsed,
    };
    readonly Button _atualizar = new() { Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(0, 0, 8, 0), Visibility = Visibility.Collapsed };
    readonly Button _fechar = new() { Padding = new Thickness(16, 6, 16, 6) };
    Atualizador.Info? _info;

    public event Action? Pronto;

    public JanelaAtualizacao()
    {
        Title = Idioma.T("upd_titulo");
        Width = 520;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new FontFamily("Segoe UI");
        Background = Brushes.White;
        Topmost = true;

        _atualizar.Content = Idioma.T("upd_botao");
        _fechar.Content = Idioma.T("upd_fechar");
        _fechar.Click += (_, _) => Close();
        _atualizar.Click += async (_, _) =>
        {
            if (_info == null) return;
            _atualizar.IsEnabled = false;
            _status.Text = Idioma.T("upd_baixando");
            string? erro;
            try { erro = await Atualizador.BaixarEPreparar(_info); }
            catch (Exception e) { erro = e.Message; }
            if (erro != null)
            {
                _status.Text = Idioma.T("upd_erro", erro);
                _atualizar.IsEnabled = true;
                return;
            }
            _status.Text = Idioma.T("upd_pronto", _info.Ultima ?? "");
            await System.Threading.Tasks.Task.Delay(1200);
            Pronto?.Invoke();
        };

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        botoes.Children.Add(_atualizar);
        botoes.Children.Add(_fechar);
        var corpo = new StackPanel { Margin = new Thickness(22) };
        corpo.Children.Add(_status);
        corpo.Children.Add(_notas);
        corpo.Children.Add(botoes);
        Content = corpo;

        Loaded += async (_, _) => await Procurar();
    }

    async System.Threading.Tasks.Task Procurar()
    {
        _status.Text = Idioma.T("upd_procurando");
        _info = await Atualizador.Checar();
        if (_info.Erro != null) { _status.Text = Idioma.T("upd_erro", _info.Erro); return; }
        if (!_info.Nova) { _status.Text = Idioma.T("upd_em_dia", _info.Atual); return; }
        _status.Text = Idioma.T("upd_nova", _info.Ultima ?? "", _info.Atual);
        if (!string.IsNullOrWhiteSpace(_info.Notas)) { _notas.Text = _info.Notas; _notas.Visibility = Visibility.Visible; }
        if (Atualizador.CopiaDeDesenvolvimento) { _status.Text += "\n" + Idioma.T("upd_dev"); return; }
        _atualizar.Visibility = Visibility.Visible;
    }
}
