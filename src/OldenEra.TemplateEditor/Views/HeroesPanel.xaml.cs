using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OldenEra.Generator.Services;

namespace OldenEra.TemplateEditor.Views;

public partial class HeroesPanel : UserControl
{
    // Per-faction CheckBox map for hero bans, keyed by hero id.
    private readonly Dictionary<string, CheckBox> _heroBanCheckBoxes = new();

    // Round-tripped pinned starting hero map. The UI for editing this is hidden
    // because the .rmg.json schema has no field for it; we keep the in-memory
    // dictionary so loading and re-saving an .oetgs file preserves the data.
    private Dictionary<string, string?> _fixedStartingHeroByFaction = new();

    // Per-school CheckBox map for spell bans, keyed by spell id.
    private readonly Dictionary<string, CheckBox> _spellBanCheckBoxes = new();

    // T-804: search-filter haystack per CheckBox. The dictionary is keyed by
    // the CheckBox itself so the filter can match against display text + id +
    // optional metadata (specialty for heroes, tier label for spells) without
    // re-querying the catalog on every keystroke.
    private readonly Dictionary<CheckBox, string[]> _heroBanHaystacks = new();
    private readonly Dictionary<CheckBox, string[]> _spellBanHaystacks = new();

    // Per-faction ComboBox for the pinned starting hero picker (T-808).
    private readonly Dictionary<string, ComboBox> _fixedStartingHeroPickers = new();

    public HeroesPanel()
    {
        InitializeComponent();
        BuildHeroBanTabs();
        BuildSpellBanTabs();
        BuildFixedStartingHeroPickers();
    }

    // T-808: one row per faction with a hero ComboBox. Selection writes
    // directly into _fixedStartingHeroByFaction so existing Get/Apply paths
    // see the user's edits without needing additional plumbing.
    private void BuildFixedStartingHeroPickers()
    {
        PnlFixedStartingHeroes.Children.Clear();
        _fixedStartingHeroPickers.Clear();

        foreach (var faction in CommunityCatalog.Default.Factions)
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var label = new Label { Content = faction.Name, Padding = new Thickness(5, 2, 5, 2) };
            Grid.SetColumn(label, 0);
            row.Children.Add(label);

            var combo = new ComboBox
            {
                DisplayMemberPath = "Display",
                SelectedValuePath = "Id",
                Tag = faction.Id,
            };

            var items = new List<HeroPickItem>
            {
                new() { Id = string.Empty, Display = "(no pin — random from faction pool)" }
            };
            items.AddRange(CommunityCatalog.Default.HeroesByFaction(faction.Id)
                .OrderBy(h => h.Name)
                .Select(h => new HeroPickItem { Id = h.Id, Display = h.Name }));

            combo.ItemsSource = items;
            combo.SelectedIndex = 0;
            combo.SelectionChanged += FixedStartingHero_SelectionChanged;

            Grid.SetColumn(combo, 1);
            row.Children.Add(combo);

            PnlFixedStartingHeroes.Children.Add(row);
            _fixedStartingHeroPickers[faction.Id] = combo;
        }
    }

    private void FixedStartingHero_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox cb) return;
        if (cb.Tag is not string factionId) return;
        var heroId = cb.SelectedValue as string;
        if (string.IsNullOrEmpty(heroId))
            _fixedStartingHeroByFaction.Remove(factionId);
        else
            _fixedStartingHeroByFaction[factionId] = heroId;
    }

    private void RefreshFixedStartingHeroPickers()
    {
        foreach (var (factionId, combo) in _fixedStartingHeroPickers)
        {
            string desired = _fixedStartingHeroByFaction.TryGetValue(factionId, out var v) && !string.IsNullOrEmpty(v)
                ? v
                : string.Empty;
            // SelectedValue lookup — fallback to first item ("(no pin)") if unknown.
            combo.SelectedValue = desired;
            if (combo.SelectedValue is null)
                combo.SelectedIndex = 0;
        }
    }

    private sealed class HeroPickItem
    {
        public string Id { get; init; } = string.Empty;
        public string Display { get; init; } = string.Empty;
    }

    private void BuildHeroBanTabs()
    {
        TcHeroBans.Items.Clear();
        _heroBanCheckBoxes.Clear();
        _heroBanHaystacks.Clear();

        foreach (var faction in CommunityCatalog.Default.Factions)
        {
            var stack = new StackPanel { Margin = new Thickness(6) };
            foreach (var hero in CommunityCatalog.Default.HeroesByFaction(faction.Id)
                                                          .OrderBy(h => h.Name))
            {
                var cb = new CheckBox
                {
                    Content = string.IsNullOrWhiteSpace(hero.Specialty)
                        ? hero.Name
                        : $"{hero.Name} — {hero.Specialty}",
                    Margin = new Thickness(2),
                    ToolTip = hero.TooltipText(),
                    Tag = hero.Id,
                };
                _heroBanCheckBoxes[hero.Id] = cb;
                _heroBanHaystacks[cb] = new[] { hero.Name, hero.Id, hero.Specialty ?? string.Empty };
                stack.Children.Add(cb);
            }
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 240,
                Content = stack,
            };
            TcHeroBans.Items.Add(new TabItem { Header = faction.Name, Content = scroll });
        }
    }

    /// <summary>Read the UI state into a flat list of banned hero ids.</summary>
    public List<string> GetHeroBans()
    {
        var bans = new List<string>();
        foreach (var (id, cb) in _heroBanCheckBoxes)
        {
            if (cb.IsChecked == true) bans.Add(id);
        }
        return bans;
    }

    /// <summary>Return the round-tripped pinned-hero map for persistence.</summary>
    public Dictionary<string, string?> GetFixedStartingHeroByFaction()
        => new(_fixedStartingHeroByFaction);

    /// <summary>Apply persisted hero bans to the UI state.</summary>
    public void ApplyHeroBans(IEnumerable<string>? heroBans)
    {
        var bans = new HashSet<string>(heroBans ?? System.Array.Empty<string>(),
                                       System.StringComparer.OrdinalIgnoreCase);
        foreach (var (id, cb) in _heroBanCheckBoxes)
            cb.IsChecked = bans.Contains(id);
    }

    /// <summary>Apply persisted hero bans + pinned heroes to the UI state.</summary>
    public void ApplyHeroSelection(IEnumerable<string>? heroBans,
                                   IDictionary<string, string?>? fixedHeroes)
    {
        ApplyHeroBans(heroBans);
        _fixedStartingHeroByFaction = fixedHeroes is null
            ? new Dictionary<string, string?>()
            : new Dictionary<string, string?>(fixedHeroes);
        RefreshFixedStartingHeroPickers();
    }

    private void BuildSpellBanTabs()
    {
        TcSpellBans.Items.Clear();
        _spellBanCheckBoxes.Clear();
        _spellBanHaystacks.Clear();

        foreach (var school in CommunityCatalog.Default.SpellSchools)
        {
            var stack = new StackPanel { Margin = new Thickness(6) };
            var spells = CommunityCatalog.Default.SpellsBySchool(school)
                .OrderBy(s => s.Tier)
                .ThenBy(s => s.Name, System.StringComparer.OrdinalIgnoreCase);

            foreach (var spell in spells)
            {
                var cb = new CheckBox
                {
                    Content = $"T{spell.Tier}. {spell.Name}",
                    Margin = new Thickness(2),
                    ToolTip = spell.TooltipText(),
                    Tag = spell.Id,
                };
                _spellBanCheckBoxes[spell.Id] = cb;
                _spellBanHaystacks[cb] = new[] { spell.Name, spell.Id, $"T{spell.Tier}" };
                stack.Children.Add(cb);
            }
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 240,
                Content = stack,
            };
            TcSpellBans.Items.Add(new TabItem { Header = CommunityCatalog.FriendlySpellSchool(school), Content = scroll });
        }
    }

    /// <summary>Read the UI state into a flat list of banned spell ids.</summary>
    public List<string> GetBannedSpells()
    {
        var bans = new List<string>();
        foreach (var (id, cb) in _spellBanCheckBoxes)
            if (cb.IsChecked == true) bans.Add(id);
        return bans;
    }

    /// <summary>Apply persisted spell bans to the UI state.</summary>
    public void ApplyBannedSpells(IEnumerable<string>? bannedSpells)
    {
        var bans = new HashSet<string>(bannedSpells ?? System.Array.Empty<string>(),
                                       System.StringComparer.OrdinalIgnoreCase);
        foreach (var (id, cb) in _spellBanCheckBoxes)
            cb.IsChecked = bans.Contains(id);
    }

    // ── T-804: substring search filters ─────────────────────────────────────
    // Empty filter = all rows visible (preserves prior behavior). Match runs
    // against name + id (+ specialty / tier) via the shared PickerFilter helper
    // so filter semantics stay identical to the Web host.

    private void TxtHeroBanFilter_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        => ApplyFilter(_heroBanHaystacks, TxtHeroBanFilter.Text);

    private void TxtSpellBanFilter_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        => ApplyFilter(_spellBanHaystacks, TxtSpellBanFilter.Text);

    private static void ApplyFilter(Dictionary<CheckBox, string[]> haystacks, string? filter)
    {
        foreach (var (cb, hay) in haystacks)
        {
            cb.Visibility = PickerFilter.Matches(filter, hay)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

}
