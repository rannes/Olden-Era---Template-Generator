using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using OldenEra.Generator.Models;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;

namespace OldenEra.TemplateEditor.Views;

public partial class ExperimentalPanel : UserControl
{
    /// <summary>Item shape for spell ComboBox; flat sorted list "{Tier}. {Name} ({School})".</summary>
    public sealed record SpellOption(string Id, string Display)
    {
        // Default record ToString prints "SpellOption { Id=..., Display=... }".
        // The custom ComboBox template's SelectionBoxItem falls back to ToString
        // when the auto-generated DisplayMemberPath template isn't picked up,
        // so render the friendly label instead.
        public override string ToString() => Display;
    }

    /// <summary>Row in the catalog-driven ban-unit ComboBox.</summary>
    /// <remarks>
    /// T-602: <see cref="Tooltip"/> carries the multi-line stats summary
    /// rendered by <c>UnitEntry.TooltipText()</c>. Bound to ToolTip on the
    /// ComboBoxItem template so hovering a row reveals tier-correct stats.
    /// </remarks>
    public sealed record BanUnitRow(string Id, string Display, string Faction, string Tooltip)
    {
        public override string ToString() => Display;
    }

    public ExperimentalPanel()
    {
        InitializeComponent();
        PopulateBanUnitPicker();

        // Populate building-preset combos. Index 0 = "(default)".
        var presets = new System.Collections.Generic.List<string> { "(default)" };
        presets.AddRange(KnownValues.BuildingsConstructionSids);
        CmbPlayerPreset.ItemsSource = presets;
        CmbNeutralPreset.ItemsSource = presets;
        CmbLowTierPreset.ItemsSource = presets;
        CmbMediumTierPreset.ItemsSource = presets;
        CmbHighTierPreset.ItemsSource = presets;
        CmbPlayerPreset.SelectedIndex = 0;
        CmbNeutralPreset.SelectedIndex = 0;
        CmbLowTierPreset.SelectedIndex = 0;
        CmbMediumTierPreset.SelectedIndex = 0;
        CmbHighTierPreset.SelectedIndex = 0;

        // Populate the bonus-spell combo from the community catalog.
        // Index 0 = sentinel (no bonus spell, value="").
        var spells = new System.Collections.Generic.List<SpellOption>
        {
            new("", "(no bonus spell)"),
        };
        spells.AddRange(
            CommunityCatalog.Default.Spells
                .OrderBy(s => s.School, System.StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Tier)
                .ThenBy(s => s.Name, System.StringComparer.OrdinalIgnoreCase)
                .Select(s => new SpellOption(s.Id, $"T{s.Tier}. {s.Name} ({s.School})")));
        CmbBonusSpell.ItemsSource = spells;
        CmbBonusSpell.SelectedIndex = 0;

        // Populate road-type combo. Index 0 = "(default)" (no override).
        var roadTypes = new System.Collections.Generic.List<string> { "(default)" };
        roadTypes.AddRange(KnownValues.RoadTypes);
        CmbRoadType.ItemsSource = roadTypes;
        CmbRoadType.SelectedIndex = 0;

        // Live label updates for sliders.
        SldBonusGold.ValueChanged       += (_, _) => TxtBonusGold.Text       = ((int)SldBonusGold.Value).ToString();
        SldBonusWood.ValueChanged       += (_, _) => TxtBonusWood.Text       = ((int)SldBonusWood.Value).ToString();
        SldBonusOre.ValueChanged        += (_, _) => TxtBonusOre.Text        = ((int)SldBonusOre.Value).ToString();
        SldBonusMercury.ValueChanged    += (_, _) => TxtBonusMercury.Text    = ((int)SldBonusMercury.Value).ToString();
        SldBonusCrystals.ValueChanged   += (_, _) => TxtBonusCrystals.Text   = ((int)SldBonusCrystals.Value).ToString();
        SldBonusGemstones.ValueChanged  += (_, _) => TxtBonusGemstones.Text  = ((int)SldBonusGemstones.Value).ToString();
        SldBonusAttack.ValueChanged     += (_, _) => TxtBonusAttack.Text     = ((int)SldBonusAttack.Value).ToString();
        SldBonusDefense.ValueChanged    += (_, _) => TxtBonusDefense.Text    = ((int)SldBonusDefense.Value).ToString();
        SldBonusSpellpower.ValueChanged += (_, _) => TxtBonusSpellpower.Text = ((int)SldBonusSpellpower.Value).ToString();
        SldBonusKnowledge.ValueChanged  += (_, _) => TxtBonusKnowledge.Text  = ((int)SldBonusKnowledge.Value).ToString();
        SldBonusUnitMultiplier.ValueChanged += (_, _) => TxtBonusUnitMultiplier.Text = ((int)SldBonusUnitMultiplier.Value).ToString();

        SldTerrainObstacles.ValueChanged    += (_, _) => TxtTerrainObstacles.Text    = ((int)SldTerrainObstacles.Value).ToString();
        SldTerrainLakes.ValueChanged        += (_, _) => TxtTerrainLakes.Text        = ((int)SldTerrainLakes.Value).ToString();
        SldBorderCornerRadius.ValueChanged    += (_, _) => TxtBorderCornerRadius.Text    = ((int)SldBorderCornerRadius.Value).ToString();
        SldBorderObstaclesWidth.ValueChanged  += (_, _) => TxtBorderObstaclesWidth.Text  = ((int)SldBorderObstaclesWidth.Value).ToString();
        SldWaterWidth.ValueChanged            += (_, _) => TxtWaterWidth.Text            = ((int)SldWaterWidth.Value).ToString();
        SldZoneGuardWeekly.ValueChanged     += (_, _) => TxtZoneGuardWeekly.Text     = ((int)SldZoneGuardWeekly.Value).ToString();
        SldConnectionGuardWeekly.ValueChanged += (_, _) => TxtConnectionGuardWeekly.Text = ((int)SldConnectionGuardWeekly.Value).ToString();
        SldNeutralGuardChance.ValueChanged  += (_, _) => TxtNeutralGuardChance.Text  = ((int)SldNeutralGuardChance.Value).ToString();
        SldEncounterHolesAffected.ValueChanged += (_, _) => TxtEncounterHolesAffected.Text = ((int)SldEncounterHolesAffected.Value).ToString();
        SldEncounterHolesTwoHole.ValueChanged  += (_, _) => TxtEncounterHolesTwoHole.Text  = ((int)SldEncounterHolesTwoHole.Value).ToString();
        SldConnectionLength.ValueChanged    += (_, _) => TxtConnectionLength.Text    = ((int)SldConnectionLength.Value).ToString();
        // Connection-default combos: index 0 = "(unset)" sentinel.
        var gatePlacements = new System.Collections.Generic.List<string> { "(unset)" };
        gatePlacements.AddRange(KnownValues.GatePlacements);
        CmbConnectionGatePlacement.ItemsSource  = gatePlacements;
        CmbConnectionGatePlacement.SelectedIndex = 0;
        CmbConnectionGuardEscape.ItemsSource     = new System.Collections.Generic.List<string> { "(unset)", "false", "true" };
        CmbConnectionGuardEscape.SelectedIndex   = 0;
        CmbConnectionSimTurnSquad.ItemsSource    = new System.Collections.Generic.List<string> { "(unset)", "false", "true" };
        CmbConnectionSimTurnSquad.SelectedIndex  = 0;
        // Mirror Blazor: contentBiomeArg is only meaningful for MatchMainObject / FromList.
        // For "" (auto) and "MatchZone" the arg has no schema role — clear + disable it.
        CmbZoneContentBiome.SelectionChanged += (_, _) => UpdateZoneContentBiomeArgState();
        UpdateZoneContentBiomeArgState();
        // T-203: same arg-relevance rules for metaObjectsBiome.
        CmbZoneMetaObjectsBiome.SelectionChanged += (_, _) => UpdateZoneMetaObjectsBiomeArgState();
        UpdateZoneMetaObjectsBiomeArgState();
        SldNeutralGuardValue.ValueChanged   += (_, _) => TxtNeutralGuardValue.Text   = ((int)SldNeutralGuardValue.Value).ToString();
        SldLowTierGuardWeekly.ValueChanged    += (_, _) => TxtLowTierGuardWeekly.Text    = ((int)SldLowTierGuardWeekly.Value).ToString();
        SldMediumTierGuardWeekly.ValueChanged += (_, _) => TxtMediumTierGuardWeekly.Text = ((int)SldMediumTierGuardWeekly.Value).ToString();
        SldHighTierGuardWeekly.ValueChanged   += (_, _) => TxtHighTierGuardWeekly.Text   = ((int)SldHighTierGuardWeekly.Value).ToString();
        SldLowTierObstacles.ValueChanged    += (_, _) => TxtLowTierObstacles.Text    = ((int)SldLowTierObstacles.Value).ToString();
        SldMediumTierObstacles.ValueChanged += (_, _) => TxtMediumTierObstacles.Text = ((int)SldMediumTierObstacles.Value).ToString();
        SldHighTierObstacles.ValueChanged   += (_, _) => TxtHighTierObstacles.Text   = ((int)SldHighTierObstacles.Value).ToString();
        SldLowTierLakes.ValueChanged    += (_, _) => TxtLowTierLakes.Text    = ((int)SldLowTierLakes.Value).ToString();
        SldMediumTierLakes.ValueChanged += (_, _) => TxtMediumTierLakes.Text = ((int)SldMediumTierLakes.Value).ToString();
        SldHighTierLakes.ValueChanged   += (_, _) => TxtHighTierLakes.Text   = ((int)SldHighTierLakes.Value).ToString();
    }

    /// <summary>
    /// Mirrors the Blazor host: contentBiomeArg is only relevant when the
    /// selector is MatchMainObject (numeric index) or FromList (biome name).
    /// For "" (auto, generator default) and MatchZone the arg has no schema
    /// role, so clear it and disable the box to avoid silently emitting junk.
    /// </summary>
    private void UpdateZoneContentBiomeArgState()
    {
        var tag = (CmbZoneContentBiome.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string ?? "";
        bool argRelevant = tag is "MatchMainObject" or "FromList";
        TxtZoneContentBiomeArg.IsEnabled = argRelevant;
        if (!argRelevant) TxtZoneContentBiomeArg.Text = "";
    }

    /// <summary>
    /// T-203: mirrors <see cref="UpdateZoneContentBiomeArgState"/> for the
    /// metaObjectsBiome selector. Same schema shape, same arg-relevance rules.
    /// </summary>
    private void UpdateZoneMetaObjectsBiomeArgState()
    {
        var tag = (CmbZoneMetaObjectsBiome.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string ?? "";
        bool argRelevant = tag is "MatchMainObject" or "FromList";
        TxtZoneMetaObjectsBiomeArg.IsEnabled = argRelevant;
        if (!argRelevant) TxtZoneMetaObjectsBiomeArg.Text = "";
    }

    /// <summary>
    /// Single-Hero mode is mutually exclusive with hero-hire-ban: enabling
    /// SingleHero clears and disables the hire-ban checkbox.
    /// </summary>
    private void ChkSingleHero_Changed(object sender, RoutedEventArgs e)
    {
        bool on = ChkSingleHero.IsChecked == true;
        ChkHeroHireBan.IsEnabled = !on;
        if (on) ChkHeroHireBan.IsChecked = false;
    }

    /// <summary>
    /// Populates the unit-ban picker with rows grouped by faction, sorted by
    /// (tier, name). Selecting a row appends its unit id to TxtGlobalBans.
    /// </summary>
    private void PopulateBanUnitPicker()
    {
        var catalog = CommunityCatalog.Default;
        var factionNames = catalog.Factions.ToDictionary(f => f.Id, f => f.Name,
            System.StringComparer.OrdinalIgnoreCase);

        var rows = catalog.Units
            .OrderBy(u => u.Faction)
            .ThenBy(u => u.Tier)
            .ThenBy(u => u.Name, System.StringComparer.OrdinalIgnoreCase)
            .Select(u => new BanUnitRow(
                u.Id,
                $"T{u.Tier}. {u.Name}" + (string.IsNullOrEmpty(u.Variant) ? "" : $" ({u.Variant})"),
                factionNames.TryGetValue(u.Faction, out var n) ? n : u.Faction,
                u.TooltipText()))
            .ToList();

        var view = CollectionViewSource.GetDefaultView(rows);
        view.GroupDescriptions.Clear();
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(BanUnitRow.Faction)));
        CmbBanUnitPicker.ItemsSource = view;
    }

    // ── T-206: per-player Starting Bonuses overrides ──────────────────────
    // Rows are stored as PerPlayerBonusFile (the persistence shape) so save/load
    // is a straight assignment from MainWindow. The UI is rebuilt imperatively
    // from this list each time it changes — keeps state ownership in one place.
    private List<PerPlayerBonusFile> _bonusOverrides = new();
    private int _bonusPlayerCount = 2;

    /// <summary>Replace the stored override rows and rebuild the UI.</summary>
    public void SetBonusPerPlayerOverrides(IEnumerable<PerPlayerBonusFile>? rows, int playerCount)
    {
        _bonusPlayerCount = System.Math.Max(1, playerCount);
        _bonusOverrides = rows is null
            ? new List<PerPlayerBonusFile>()
            : rows.Select(r => new PerPlayerBonusFile
            {
                PlayerSlot = r.PlayerSlot,
                Resources = r.Resources is null ? new() : new Dictionary<string,int>(r.Resources),
                HeroAttack = r.HeroAttack,
                HeroDefense = r.HeroDefense,
                HeroSpellpower = r.HeroSpellpower,
                HeroKnowledge = r.HeroKnowledge,
                HeroStatStartHeroOnly = r.HeroStatStartHeroOnly,
                ItemSid = r.ItemSid ?? "",
                ItemStartHeroOnly = r.ItemStartHeroOnly,
                SpellSid = r.SpellSid ?? "",
                SpellStartHeroOnly = r.SpellStartHeroOnly,
                UnitMultiplier = r.UnitMultiplier,
                UnitMultiplierStartHeroOnly = r.UnitMultiplierStartHeroOnly,
            }).ToList();
        RebuildBonusOverrideRows();
    }

    /// <summary>Snapshot the current rows for persistence.</summary>
    public List<PerPlayerBonusFile> GetBonusPerPlayerOverrides() =>
        _bonusOverrides.Select(r => new PerPlayerBonusFile
        {
            PlayerSlot = r.PlayerSlot,
            Resources = new Dictionary<string,int>(r.Resources),
            HeroAttack = r.HeroAttack,
            HeroDefense = r.HeroDefense,
            HeroSpellpower = r.HeroSpellpower,
            HeroKnowledge = r.HeroKnowledge,
            HeroStatStartHeroOnly = r.HeroStatStartHeroOnly,
            ItemSid = r.ItemSid,
            ItemStartHeroOnly = r.ItemStartHeroOnly,
            SpellSid = r.SpellSid,
            SpellStartHeroOnly = r.SpellStartHeroOnly,
            UnitMultiplier = r.UnitMultiplier,
            UnitMultiplierStartHeroOnly = r.UnitMultiplierStartHeroOnly,
        }).ToList();

    public void RefreshBonusOverridePlayerCount(int playerCount)
    {
        _bonusPlayerCount = System.Math.Max(1, playerCount);
        // Clamp the model so a row created at slot 8 doesn't silently keep its
        // old value when the player slider drops to 4. Without this clamp the
        // ComboBox display clamps but the saved settings file still stores 8,
        // and the validator warns on every save.
        foreach (var row in _bonusOverrides)
        {
            if (row.PlayerSlot < 1) row.PlayerSlot = 1;
            else if (row.PlayerSlot > _bonusPlayerCount) row.PlayerSlot = _bonusPlayerCount;
        }
        RebuildBonusOverrideRows();
    }

    private void BonusPerPlayerAdd_Click(object sender, RoutedEventArgs e)
    {
        var used = new HashSet<int>(_bonusOverrides.Select(r => r.PlayerSlot));
        int slot = 1;
        for (; slot <= _bonusPlayerCount; slot++)
            if (!used.Contains(slot)) break;
        if (slot > _bonusPlayerCount) slot = 1;
        _bonusOverrides.Add(new PerPlayerBonusFile { PlayerSlot = slot });
        RebuildBonusOverrideRows();
    }

    private void RebuildBonusOverrideRows()
    {
        PnlBonusPerPlayerOverrides.Children.Clear();
        for (int i = 0; i < _bonusOverrides.Count; i++)
        {
            int idx = i;
            var row = _bonusOverrides[idx];
            PnlBonusPerPlayerOverrides.Children.Add(BuildBonusOverrideRow(idx, row));
        }
    }

    private FrameworkElement BuildBonusOverrideRow(int idx, PerPlayerBonusFile row)
    {
        var border = new System.Windows.Controls.Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x88, 0xAA, 0xAA, 0xAA)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6),
            Margin = new Thickness(0, 4, 0, 0),
        };
        var sp = new StackPanel();
        border.Child = sp;

        // Header: slot picker + remove
        var header = new DockPanel { LastChildFill = false };
        header.Children.Add(new TextBlock { Text = "Player slot ", VerticalAlignment = VerticalAlignment.Center });
        var slotCombo = new ComboBox { Width = 60, VerticalAlignment = VerticalAlignment.Center };
        for (int s = 1; s <= _bonusPlayerCount; s++) slotCombo.Items.Add(s);
        slotCombo.SelectedItem = System.Math.Clamp(row.PlayerSlot, 1, _bonusPlayerCount);
        slotCombo.SelectionChanged += (_, _) =>
        {
            if (slotCombo.SelectedItem is int v) row.PlayerSlot = v;
        };
        header.Children.Add(slotCombo);
        var removeBtn = new Button { Content = "Remove", Margin = new Thickness(8, 0, 0, 0) };
        DockPanel.SetDock(removeBtn, Dock.Right);
        removeBtn.Click += (_, _) =>
        {
            if (idx >= 0 && idx < _bonusOverrides.Count)
            {
                _bonusOverrides.RemoveAt(idx);
                RebuildBonusOverrideRows();
            }
        };
        header.Children.Add(removeBtn);
        sp.Children.Add(header);

        // Resources (gold + wood are the most common; rarer ones can use the uniform block).
        sp.Children.Add(MakeIntRow("Gold", row.Resources.TryGetValue("gold", out var g) ? g : 0,
            v => SetResource(row, "gold", v)));
        sp.Children.Add(MakeIntRow("Wood", row.Resources.TryGetValue("wood", out var w) ? w : 0,
            v => SetResource(row, "wood", v)));
        sp.Children.Add(MakeIntRow("Ore", row.Resources.TryGetValue("ore", out var o) ? o : 0,
            v => SetResource(row, "ore", v)));
        sp.Children.Add(MakeIntRow("Mercury", row.Resources.TryGetValue("mercury", out var m) ? m : 0,
            v => SetResource(row, "mercury", v)));
        sp.Children.Add(MakeIntRow("Crystals", row.Resources.TryGetValue("crystals", out var c) ? c : 0,
            v => SetResource(row, "crystals", v)));
        sp.Children.Add(MakeIntRow("Gemstones", row.Resources.TryGetValue("gemstones", out var gm) ? gm : 0,
            v => SetResource(row, "gemstones", v)));

        // Hero stats
        sp.Children.Add(MakeIntRow("Hero Attack", row.HeroAttack, v => row.HeroAttack = v));
        sp.Children.Add(MakeIntRow("Hero Defense", row.HeroDefense, v => row.HeroDefense = v));
        sp.Children.Add(MakeIntRow("Hero Spellpower", row.HeroSpellpower, v => row.HeroSpellpower = v));
        sp.Children.Add(MakeIntRow("Hero Knowledge", row.HeroKnowledge, v => row.HeroKnowledge = v));
        sp.Children.Add(MakeCheckRow("Hero stats: start hero only",
            row.HeroStatStartHeroOnly, v => row.HeroStatStartHeroOnly = v));

        // Item / Spell
        sp.Children.Add(MakeStringRow("Item SID", row.ItemSid, v => row.ItemSid = v));
        sp.Children.Add(MakeCheckRow("Item: start hero only",
            row.ItemStartHeroOnly, v => row.ItemStartHeroOnly = v));
        sp.Children.Add(MakeStringRow("Spell SID", row.SpellSid, v => row.SpellSid = v));
        sp.Children.Add(MakeCheckRow("Spell: start hero only",
            row.SpellStartHeroOnly, v => row.SpellStartHeroOnly = v));

        // Unit multiplier (percent)
        sp.Children.Add(MakeIntRow("Unit multiplier (%)",
            (int)System.Math.Round(row.UnitMultiplier * 100.0),
            v => row.UnitMultiplier = v / 100.0));
        sp.Children.Add(MakeCheckRow("Unit multiplier: start hero only",
            row.UnitMultiplierStartHeroOnly, v => row.UnitMultiplierStartHeroOnly = v));

        return border;
    }

    private static void SetResource(PerPlayerBonusFile row, string sid, int value)
    {
        if (value <= 0) row.Resources.Remove(sid);
        else row.Resources[sid] = value;
    }

    private static Grid MakeIntRow(string label, int initial, System.Action<int> onChanged)
    {
        var g = new Grid { Margin = new Thickness(0, 2, 0, 0) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        g.Children.Add(new Label { Content = label });
        var tb = new TextBox { Text = initial.ToString(CultureInfo.InvariantCulture) };
        Grid.SetColumn(tb, 1);
        tb.TextChanged += (_, _) =>
        {
            onChanged(int.TryParse(tb.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0);
        };
        g.Children.Add(tb);
        return g;
    }

    private static Grid MakeStringRow(string label, string initial, System.Action<string> onChanged)
    {
        var g = new Grid { Margin = new Thickness(0, 2, 0, 0) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.Children.Add(new Label { Content = label });
        var tb = new TextBox { Text = initial ?? "" };
        Grid.SetColumn(tb, 1);
        tb.TextChanged += (_, _) => onChanged(tb.Text ?? "");
        g.Children.Add(tb);
        return g;
    }

    private static CheckBox MakeCheckRow(string label, bool initial, System.Action<bool> onChanged)
    {
        var cb = new CheckBox { Content = label, IsChecked = initial, Margin = new Thickness(0, 2, 0, 0) };
        cb.Checked += (_, _) => onChanged(true);
        cb.Unchecked += (_, _) => onChanged(false);
        return cb;
    }

    private void CmbBanUnitPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbBanUnitPicker.SelectedItem is not BanUnitRow row) return;
        var current = (TxtGlobalBans.Text ?? string.Empty).Trim();
        var existing = current
            .Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
        if (!existing.Contains(row.Id, System.StringComparer.OrdinalIgnoreCase))
        {
            existing.Add(row.Id);
            TxtGlobalBans.Text = string.Join(", ", existing);
        }
        // Reset selection so the same item can be re-picked next time the user
        // clears it from the textbox.
        CmbBanUnitPicker.SelectedIndex = -1;
    }
}
