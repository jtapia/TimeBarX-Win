using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using TimeBarX.Core;

namespace TimeBarX.App;

/// <summary>
/// CRUD for the Pro-only custom-preset list. Read-only view items (<see
/// cref="PresetRow"/>) carry both the preset and its index so the Remove
/// button can address the underlying list by position — simpler than tracking
/// reference identity across re-binds.
/// </summary>
public partial class ManagePresetsDialog : Window
{
    private readonly TrayController? _controller;
    private ItemsControl? _list;
    private TextBox? _nameInput;
    private TextBox? _durationInput;
    private TextBlock? _emptyHint;
    private TextBlock? _status;

    public ManagePresetsDialog() : this(null) { }

    public ManagePresetsDialog(TrayController? controller)
    {
        InitializeComponent();
        _controller = controller;
        _list = this.FindControl<ItemsControl>("PresetList");
        _nameInput = this.FindControl<TextBox>("NameInput");
        _durationInput = this.FindControl<TextBox>("DurationInput");
        _emptyHint = this.FindControl<TextBlock>("EmptyHint");
        _status = this.FindControl<TextBlock>("StatusText");
        Refresh();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void Refresh()
    {
        var presets = _controller?.Settings.CustomPresets ?? Array.Empty<CustomPreset>();
        if (_list is not null)
        {
            _list.ItemsSource = presets
                .Select((p, i) => new PresetRow(i, p))
                .ToList();
        }
        if (_emptyHint is not null) _emptyHint.IsVisible = presets.Count == 0;
    }

    private void OnAddClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_controller is null) return;
        var name = _nameInput?.Text?.Trim() ?? string.Empty;
        var raw = _durationInput?.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name))
        {
            ShowStatus("Give the preset a name.");
            return;
        }
        if (!DurationParser.TryParse(raw, out var parsed) || parsed.Duration <= TimeSpan.Zero)
        {
            ShowStatus("Couldn't parse duration. Try \"15 min\", \"1:30\", or \"2h\".");
            return;
        }

        var preset = new CustomPreset(name, parsed.Duration, parsed.Label);
        if (!preset.IsValid)
        {
            ShowStatus("Preset is invalid.");
            return;
        }

        _controller.UpdateSettings(s =>
        {
            var next = new List<CustomPreset>(s.CustomPresets ?? Array.Empty<CustomPreset>()) { preset };
            return s with { CustomPresets = next };
        });

        if (_nameInput is not null) _nameInput.Text = string.Empty;
        if (_durationInput is not null) _durationInput.Text = string.Empty;
        ClearStatus();
        Refresh();
    }

    private void OnRemoveClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_controller is null) return;
        if (sender is not Button btn || btn.Tag is not int index) return;

        _controller.UpdateSettings(s =>
        {
            var list = new List<CustomPreset>(s.CustomPresets ?? Array.Empty<CustomPreset>());
            if (index < 0 || index >= list.Count) return s;
            list.RemoveAt(index);
            return s with { CustomPresets = list };
        });
        Refresh();
    }

    private void ShowStatus(string text)
    {
        if (_status is null) return;
        _status.Text = text;
        _status.IsVisible = true;
    }

    private void ClearStatus()
    {
        if (_status is null) return;
        _status.Text = string.Empty;
        _status.IsVisible = false;
    }

    /// <summary>View row for the list binding. Carries the index so the Remove
    /// button can target the correct slot even after re-binds.</summary>
    public sealed record PresetRow(int Index, CustomPreset Preset)
    {
        public string Name => Preset.Name;
        public string DurationDisplay => FormatDuration(Preset.Duration);
        public string LabelDisplay => string.IsNullOrWhiteSpace(Preset.Label) ? string.Empty : $"· {Preset.Label}";

        private static string FormatDuration(TimeSpan d)
        {
            if (d.TotalHours >= 1 && d.Minutes == 0 && d.Seconds == 0) return $"{(int)d.TotalHours} h";
            if (d.TotalMinutes >= 1 && d.Seconds == 0) return $"{(int)d.TotalMinutes} min";
            return d.ToString();
        }
    }
}
