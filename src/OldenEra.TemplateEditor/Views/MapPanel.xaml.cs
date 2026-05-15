using System;
using System.Windows;
using System.Windows.Controls;
using OldenEra.Generator.Services;
using OldenEra.TemplateEditor.Services;

namespace OldenEra.TemplateEditor.Views;

public partial class MapPanel : UserControl
{
    public MapPanel()
    {
        InitializeComponent();
        AttachFieldHelp();
    }

    // T-803: pull tooltips from docs/field-help.yaml so the WPF host shows
    // the same inline help as the Web build.
    private void AttachFieldHelp()
    {
        FieldHelpAttacher.Attach(TxtTemplateName, ValidationFieldKeys.TemplateName, LblTemplateName);
        FieldHelpAttacher.Attach(TxtSeed, FieldHelpKeys.Seed, LblSeed);
        FieldHelpAttacher.Attach(CmbMapSize, ValidationFieldKeys.MapSize, LblMapSize);
        FieldHelpAttacher.Attach(SldPlayers, ValidationFieldKeys.PlayerCount, LblPlayers);
    }

    private void BtnRandomizeSeed_Click(object sender, RoutedEventArgs e)
    {
        TxtSeed.Text = Random.Shared.Next(0, int.MaxValue).ToString();
    }
}
