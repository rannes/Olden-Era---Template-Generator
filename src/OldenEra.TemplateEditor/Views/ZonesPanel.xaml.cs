using System.Windows.Controls;
using OldenEra.Generator.Services;
using OldenEra.TemplateEditor.Services;

namespace OldenEra.TemplateEditor.Views;

public partial class ZonesPanel : UserControl
{
    public ZonesPanel()
    {
        InitializeComponent();
        AttachFieldHelp();
    }

    // T-803: pull tooltips from docs/field-help.yaml.
    private void AttachFieldHelp()
    {
        FieldHelpAttacher.Attach(SldNeutral, ValidationFieldKeys.NeutralZoneCount);
        FieldHelpAttacher.Attach(SldPlayerCastles, ValidationFieldKeys.PlayerZoneCastles);
        FieldHelpAttacher.Attach(SldPlayerZoneSize, FieldHelpKeys.PlayerZoneSize);
        FieldHelpAttacher.Attach(SldNeutralZoneSize, FieldHelpKeys.NeutralZoneSize);
        FieldHelpAttacher.Attach(SldGuardRandomization, FieldHelpKeys.GuardsRandomization);
        FieldHelpAttacher.Attach(SldMinNeutralBetweenPlayers, ValidationFieldKeys.MinNeutralSeparation);
    }
}
