using System.Windows;
using System.Windows.Controls;
using OldenEra.Generator.Models;
using OldenEra.Generator.Services;

namespace OldenEra.TemplateEditor.Views;

/// <summary>
/// T-805 — Side-by-side field-level diff between the last-loaded preset and
/// the current settings. WPF parity for
/// <c>OldenEra.Web.Components.PresetDiffPanel</c>. Hidden via
/// <see cref="UIElement.Visibility"/> until <see cref="ShowFor"/> is called
/// with a preset snapshot; <see cref="Hide"/> restores the hidden state for
/// the "no preset loaded yet" hard rule.
/// </summary>
public partial class PresetDiffPanel : UserControl
{
    private SettingsFile? _presetSnapshot;
    private string _presetName = "";

    public PresetDiffPanel()
    {
        InitializeComponent();
    }

    /// <summary>Take a snapshot of the just-loaded preset and start showing the panel.</summary>
    public void ShowFor(SettingsFile presetSnapshot, string presetName)
    {
        _presetSnapshot = presetSnapshot;
        _presetName = presetName ?? "";
        TxtTitle.Text = $"Compared to preset: {_presetName}";
        Visibility = Visibility.Visible;
        // Caller is expected to follow up with Update(current) once UI state
        // is settled, but draw an empty state immediately for snappy feel.
        Update(presetSnapshot);
    }

    /// <summary>Reset to the no-preset-loaded state and hide.</summary>
    public void Hide()
    {
        _presetSnapshot = null;
        _presetName = "";
        Visibility = Visibility.Collapsed;
    }

    /// <summary>Recompute the diff against the live current settings.</summary>
    public void Update(SettingsFile? current)
    {
        if (_presetSnapshot is null || current is null)
        {
            // Nothing to compare — keep the panel hidden (the hard rule says
            // it stays hidden until a preset has been loaded this session).
            Visibility = Visibility.Collapsed;
            return;
        }

        var rows = SettingsDiff.Compute(_presetSnapshot, current);

        TxtCount.Text = $"{rows.Count} changed";
        GridRows.Children.Clear();
        GridRows.RowDefinitions.Clear();

        if (rows.Count == 0)
        {
            TxtEmpty.Visibility = Visibility.Visible;
            return;
        }

        TxtEmpty.Visibility = Visibility.Collapsed;
        AddHeaderRow();
        foreach (var r in rows)
        {
            AddDataRow(r);
        }
    }

    private void AddHeaderRow()
    {
        GridRows.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        int row = GridRows.RowDefinitions.Count - 1;
        AddCell("Field", row, 0, headerStyle: true);
        AddCell("Preset", row, 1, headerStyle: true);
        AddCell("Current", row, 2, headerStyle: true);
    }

    private void AddDataRow(SettingsDiffRow r)
    {
        GridRows.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        int row = GridRows.RowDefinitions.Count - 1;
        AddCell(r.FieldPath, row, 0, headerStyle: false, fieldStyle: true);
        AddCell(r.PresetValue, row, 1, headerStyle: false, fieldStyle: false);
        AddCell(r.CurrentValue, row, 2, headerStyle: false, fieldStyle: false);
    }

    private void AddCell(string text, int row, int col, bool headerStyle, bool fieldStyle = false)
    {
        var tb = new TextBlock { Text = text };
        var key = headerStyle ? "PdHeader" : (fieldStyle ? "PdField" : "PdValue");
        if (Resources[key] is Style s)
            tb.Style = s;
        Grid.SetRow(tb, row);
        Grid.SetColumn(tb, col);
        GridRows.Children.Add(tb);
    }
}
