using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using TimeBarX.Core;

namespace TimeBarX.App;

public partial class QuickInputWindow : Window
{
    private const string DefaultHint = "Enter to start  ·  Esc to cancel";
    private const string ParseErrorHint = "Could not parse — try \"25 min\", \"1:30\", or \"2h review PR\".";

    private TextBox? _input;
    private TextBlock? _hint;
    private Avalonia.Media.IBrush? _defaultHintBrush;

    public QuickInputWindow()
    {
        InitializeComponent();
        _input = this.FindControl<TextBox>("InputBox");
        _hint = this.FindControl<TextBlock>("HintText");
        _defaultHintBrush = _hint?.Foreground;
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
                ShowParseError();
            }
            e.Handled = true;
            return;
        }

        // Any other keystroke after a parse error: revert the hint so the user
        // doesn't keep seeing a stale red error while they're correcting input.
        if (_hint is not null && _hint.Text == ParseErrorHint)
        {
            _hint.Text = DefaultHint;
            _hint.Foreground = _defaultHintBrush;
        }
    }

    private void ShowParseError()
    {
        if (_hint is null) return;
        _hint.Text = ParseErrorHint;
        _hint.Foreground = Avalonia.Media.Brushes.IndianRed;
    }
}
