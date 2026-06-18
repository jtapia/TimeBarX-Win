using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using TimeBarX.Core;

namespace TimeBarX.App;

public partial class QuickInputWindow : Window
{
    private TextBox? _input;
    private TextBlock? _hint;

    public QuickInputWindow()
    {
        InitializeComponent();
        _input = this.FindControl<TextBox>("InputBox");
        _hint = this.FindControl<TextBlock>("HintText");
        Opened += (_, _) => _input?.Focus();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public ParsedDuration? Result { get; private set; }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Result = null;
            Close();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            var text = _input?.Text ?? string.Empty;
            if (DurationParser.TryParse(text, out var parsed))
            {
                Result = parsed;
                Close();
            }
            else
            {
                if (_hint is not null)
                {
                    _hint.Text = "Could not parse — try \"25 min\", \"1:30\", or \"2h review PR\".";
                    _hint.Foreground = Avalonia.Media.Brushes.IndianRed;
                }
            }
            e.Handled = true;
        }
    }
}
