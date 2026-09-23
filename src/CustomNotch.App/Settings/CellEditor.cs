using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using CustomNotch.Core.Config;
using CustomNotch.Core.Sources;

namespace CustomNotch.App.Settings;

/// <summary>Une cellule en quatre sections : Source (le formulaire de son schéma), Affichage, Actions, Groupe. Chaque
/// contrôle écrit tout de suite par ConfigEditor via `run` (qui affiche un refus dans le bandeau de la page).</summary>
public sealed class CellEditor : UserControl
{
    private static readonly (string, string)[] Kinds = { ("", "Automatique"), ("ring", "Anneau"), ("value", "Valeur"), ("status", "Statut"), ("sparkline", "Sparkline") };
    private static readonly (string, string)[] Refreshes = { ("", "Par défaut"), ("1s", "1 s"), ("2s", "2 s"), ("5s", "5 s"), ("30s", "30 s"), ("1m", "1 min"), ("5m", "5 min"), ("1h", "1 h") };
    private static readonly (string, string)[] ClickKinds = { ("", "Ouvrir la carte"), ("default", "Action par défaut de la source"), ("open", "Ouvrir une URL, un fichier, une app"), ("shell", "Lancer une commande"), ("source", "Une action de la source") };
    private static readonly (string, string)[] ActionKinds = { ("open", "Ouvrir…"), ("shell", "Commande…"), ("source", "Action de la source") };

    private readonly SettingsContext _ctx;
    private readonly CellConfig _cell;
    private readonly Action<Action> _run;

    public CellEditor(SettingsContext ctx, CellConfig cell, Action<Action> run)
    {
        _ctx = ctx; _cell = cell; _run = run;
        var body = new StackPanel();
        body.Children.Add(Ui.Text($"{(cell.IsGroup ? "Groupe" : "Cellule")} « {cell.Id} »", 17, FontWeights.SemiBold));
        body.Children.Add(Section("Source"));
        body.Children.Add(Card(SourceSection()));
        body.Children.Add(Section("Affichage"));
        body.Children.Add(Card(DisplaySection()));
        body.Children.Add(Section("Actions"));
        body.Children.Add(Card(ActionsSection()));
        body.Children.Add(Section("Groupe"));
        body.Children.Add(Card(GroupSection()));
        Content = body;
    }

    private SourceSchema? Schema => _cell.IsGroup ? null : _ctx.Registry.Get(_cell.Source)?.Schema;

    // ---- Source ---------------------------------------------------------------------------------------------

    private UIElement SourceSection()
    {
        var stack = new StackPanel();
        if (_cell.IsGroup)
        {
            stack.Children.Add(Ui.Text("Un groupe n'a pas de source : son anneau et sa carte viennent de ses enfants.", 12, null, "Muted"));
            return stack;
        }
        var schema = Schema;
        var head = new DockPanel();
        var change = Btn("Changer…", () =>
        {
            var type = SourceCatalogDialog.Pick(Window.GetWindow(this)!, _ctx.Registry);
            if (type is null || type == _cell.Source) return;
            var target = _ctx.Registry.Get(type)!.Schema;
            _run(() => _ctx.Editor.SetCell(_cell.Id, c =>
            {
                c["source"] = type;
                c.Remove("params");
                var defaults = new JsonObject();
                foreach (var f in target.Fields.Where(f => f.Default is not null))
                    defaults[f.Name] = f.Type switch { "number" => JsonValue.Create(double.Parse(f.Default!, CultureInfo.InvariantCulture)), "bool" => JsonValue.Create(bool.Parse(f.Default!)), _ => JsonValue.Create(f.Default!) };
                if (defaults.Count > 0) c["params"] = defaults;
                if (target.DefaultGlyph is { } g) c["glyph"] = g;
            }));
        });
        DockPanel.SetDock(change, Dock.Right);
        head.Children.Add(change);
        var title = new StackPanel();
        title.Children.Add(Ui.Text(schema?.Title ?? _cell.Source, 13, FontWeights.SemiBold));
        var desc = Ui.Text(schema?.Description ?? "source inconnue", 11, null, "Muted"); desc.TextWrapping = TextWrapping.Wrap;
        title.Children.Add(desc);
        head.Children.Add(title);
        stack.Children.Add(head);
        if (schema is null) return stack;
        var form = SchemaForm.Build(schema, _cell.Params,
            (name, node) => _run(() => _ctx.Editor.SetCell(_cell.Id, c =>
            {
                if (c["params"] is not JsonObject p) c["params"] = p = new JsonObject();
                if (node is null) p.Remove(name); else p[name] = node.DeepClone();
            })),
            (name, value) => _run(() =>
            {
                if (value is null)
                {
                    _ctx.Editor.RemoveSecret(ConfigEditor.SecretName(_cell.Id, name));
                    _ctx.Editor.SetCell(_cell.Id, c => (c["params"] as JsonObject)?.Remove(name));
                }
                else
                {
                    _ctx.Editor.SetSecret(ConfigEditor.SecretName(_cell.Id, name), value);
                    _ctx.Editor.SetCell(_cell.Id, c =>
                    {
                        if (c["params"] is not JsonObject p) c["params"] = p = new JsonObject();
                        p[name] = ConfigEditor.SecretPlaceholder(_cell.Id, name);
                    });
                }
            }));
        ((FrameworkElement)form).Margin = new Thickness(0, 10, 0, 0);
        stack.Children.Add(form);
        return stack;
    }

    // ---- Affichage ------------------------------------------------------------------------------------------

    private UIElement DisplaySection()
    {
        var stack = new StackPanel();
        stack.Children.Add(Row("Libellé", Debounced(_cell.Label ?? "", v => Set(c => { if (v.Length == 0) c.Remove("label"); else c["label"] = v; }), name: "label")));
        stack.Children.Add(Row("Glyph", GlyphGallery.Build(_cell.Glyph, g => Set(c => { if (g is null) c.Remove("glyph"); else c["glyph"] = g; }))));
        if (!_cell.IsGroup)
        {
            stack.Children.Add(Row("Type de rendu", Combo(Kinds, _cell.Kind ?? "", v => Set(c => { if (v.Length == 0) c.Remove("kind"); else c["kind"] = v; }))));
            var refresh = new StackPanel { Orientation = Orientation.Horizontal };
            var known = Refreshes.Any(r => r.Item1 == (_cell.Refresh ?? ""));
            var combo = Combo(Refreshes, known ? _cell.Refresh ?? "" : "", v => Set(c => { if (v.Length == 0) c.Remove("refresh"); else c["refresh"] = v; }));
            var custom = Debounced(known ? "" : _cell.Refresh ?? "", v => Set(c => { if (v.Trim().Length == 0) c.Remove("refresh"); else c["refresh"] = v.Trim(); }), 100, "refresh");
            custom.Margin = new Thickness(8, 0, 0, 0);
            refresh.Children.Add(combo); refresh.Children.Add(custom); refresh.Children.Add(Muted("  ou « 90s », « 2h »"));
            stack.Children.Add(Row("Cadence de lecture", refresh));
        }
        var thresholds = new StackPanel { Orientation = Orientation.Horizontal };
        // warn/crit/invert sont pré-déclarées : CommitThresholds() les capture toutes trois, y compris depuis le
        // gestionnaire passé à Debounced() dans leur propre initialisation — sans ça, le compilateur voit une
        // variable qui se référence elle-même avant d'être assignée (CS0165).
        TextBox warn = null!, crit = null!;
        CheckBox invert = null!;
        warn = Debounced(_cell.Thresholds?.Warn?.ToString(CultureInfo.InvariantCulture) ?? "", _ => CommitThresholds(), 90, "warn");
        crit = Debounced(_cell.Thresholds?.Crit?.ToString(CultureInfo.InvariantCulture) ?? "", _ => CommitThresholds(), 90, "crit");
        invert = new CheckBox { Content = "inversé (bas = mauvais)", IsChecked = _cell.Thresholds?.Invert ?? false, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        invert.Checked += (_, _) => CommitThresholds(); invert.Unchecked += (_, _) => CommitThresholds();
        thresholds.Children.Add(Muted("avertir à ")); thresholds.Children.Add(warn); thresholds.Children.Add(Muted("  critique à ")); thresholds.Children.Add(crit); thresholds.Children.Add(invert);
        stack.Children.Add(Row("Seuils (% ou valeur)", thresholds));
        return stack;

        void CommitThresholds()
        {
            var w = SchemaForm.ParseNumber(warn.Text);
            var k = SchemaForm.ParseNumber(crit.Text);
            Set(c =>
            {
                if (w is null && k is null && invert.IsChecked != true) { c.Remove("thresholds"); return; }
                var t = new JsonObject();
                if (w is { } wv) t["warn"] = wv;
                if (k is { } kv) t["crit"] = kv;
                if (invert.IsChecked == true) t["invert"] = true;
                c["thresholds"] = t;
            });
        }
    }

    // ---- Actions --------------------------------------------------------------------------------------------

    private UIElement ActionsSection()
    {
        var stack = new StackPanel();
        var click = _cell.Actions?.Click;
        var current = click is null ? (Schema?.DefaultAction is not null ? "default" : "") : click.Open is not null ? "open" : click.Shell is not null ? "shell" : click.Source is not null ? "source" : "";
        // value/kind : même pré-déclaration que warn/crit/invert plus haut, pour la même raison (CS0165).
        TextBox value = null!;
        ComboBox kind = null!;
        value = Debounced(click?.Open ?? click?.Shell ?? click?.Source ?? "", _ => CommitClick(), 320, "click");
        kind = Combo(ClickKinds, current, _ => CommitClick());
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        value.Margin = new Thickness(8, 0, 0, 0);
        panel.Children.Add(kind); panel.Children.Add(value);
        stack.Children.Add(Row("Au clic", panel));
        stack.Children.Add(Muted("« Action par défaut » : ouvrir pour un lanceur, lecture/pause pour le média."));

        var buttons = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        var list = (_cell.Actions?.Card ?? new List<ActionConfig>()).ToList();
        void Render()
        {
            buttons.Children.Clear();
            for (var i = 0; i < list.Count; i++)
            {
                var index = i; var a = list[i];
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                // Label et valeur : la ligne montre déjà ce que l'utilisateur tape, donc CommitCard(rebuild: false)
                // — reconstruire ici démonterait le TextBox en pleine frappe (nouvelle instance, focus perdu) pour
                // un résultat visuellement identique. Seul ce qui change la forme des lignes (genre, ordre,
                // ajout/suppression) reconstruit.
                row.Children.Add(Debounced(a.Label ?? "", v => { a.Label = v; CommitCard(rebuild: false); }, 140, $"act_label_{index}"));
                var k = Combo(ActionKinds, a.Open is not null ? "open" : a.Shell is not null ? "shell" : "source", v => { var val = a.Open ?? a.Shell ?? a.Source ?? ""; a.Open = a.Shell = a.Source = null; if (v == "open") a.Open = val; else if (v == "shell") a.Shell = val; else a.Source = val; CommitCard(); });
                k.Margin = new Thickness(8, 0, 0, 0); row.Children.Add(k);
                var v = Debounced(a.Open ?? a.Shell ?? a.Source ?? "", t => { if (a.Open is not null) a.Open = t; else if (a.Shell is not null) a.Shell = t; else a.Source = t; CommitCard(rebuild: false); }, 220, $"act_value_{index}");
                v.Margin = new Thickness(8, 0, 0, 0); row.Children.Add(v);
                row.Children.Add(Btn("↑", () => { if (index > 0) { (list[index - 1], list[index]) = (list[index], list[index - 1]); CommitCard(); } }, "GhostButton"));
                row.Children.Add(Btn("↓", () => { if (index < list.Count - 1) { (list[index + 1], list[index]) = (list[index], list[index + 1]); CommitCard(); } }, "GhostButton"));
                row.Children.Add(Btn("✕", () => { list.RemoveAt(index); CommitCard(); }, "GhostButton"));
                buttons.Children.Add(row);
            }
            buttons.Children.Add(Btn("+ Bouton", () => { list.Add(new ActionConfig { Label = "Ouvrir", Open = "https://" }); CommitCard(); }));
        }
        Render();
        stack.Children.Add(Row("Boutons de la carte", buttons));
        return stack;

        void CommitClick()
        {
            var k = ((ComboBoxItem)kind.SelectedItem).Tag as string ?? "";
            var v = value.Text.Trim();
            Set(c =>
            {
                if (c["actions"] is not JsonObject actions) c["actions"] = actions = new JsonObject();
                switch (k)
                {
                    case "open": actions["click"] = new JsonObject { ["open"] = v }; break;
                    case "shell": actions["click"] = new JsonObject { ["shell"] = v }; break;
                    case "source": actions["click"] = new JsonObject { ["source"] = v }; break;
                    default: actions.Remove("click"); break;   // carte, ou action par défaut de la source (= pas de click)
                }
                if (actions.Count == 0) c.Remove("actions");
            });
        }

        // rebuild = false pour une simple saisie (label, valeur) : la ligne éditée montre déjà le bon texte, nul
        // besoin de la redémonter. rebuild = true (par défaut) pour tout ce qui change la forme des lignes —
        // genre (kind), ordre (↑/↓), ajout/suppression — où la liste affichée doit vraiment changer.
        void CommitCard(bool rebuild = true)
        {
            Set(c =>
            {
                if (c["actions"] is not JsonObject actions) c["actions"] = actions = new JsonObject();
                if (list.Count == 0) actions.Remove("card");
                else actions["card"] = new JsonArray(list.Select(a =>
                {
                    var o = new JsonObject();
                    if (a.Label is { Length: > 0 } l) o["label"] = l;
                    if (a.Open is not null) o["open"] = a.Open; else if (a.Shell is not null) o["shell"] = a.Shell; else if (a.Source is not null) o["source"] = a.Source;
                    return (JsonNode)o;
                }).ToArray());
                if (actions.Count == 0) c.Remove("actions");
            });
            if (rebuild) Render();
        }
    }

    // ---- Groupe ---------------------------------------------------------------------------------------------

    private UIElement GroupSection()
    {
        var stack = new StackPanel();
        var pill = _ctx.Store.Current.Pills.FirstOrDefault(p => p.Cells.Any(c => c.Id == _cell.Id));
        var candidates = (pill?.Cells ?? new List<CellConfig>()).Where(c => c.Id != _cell.Id).ToList();
        if (candidates.Count == 0) { stack.Children.Add(Muted("Aucune autre cellule dans cette pilule.")); return stack; }
        var children = (_cell.Children ?? new List<string>()).ToList();
        var boxes = new WrapPanel { MaxWidth = 460 };
        foreach (var c in candidates)
        {
            var box = new CheckBox { Content = c.Label ?? c.Id, IsChecked = children.Contains(c.Id), Margin = new Thickness(0, 0, 16, 4) };
            box.Checked += (_, _) =>
            {
                if (!children.Contains(c.Id)) children.Add(c.Id);
                CommitChildren();
                if (c.Visible && MessageBox.Show(Window.GetWindow(this), $"Masquer « {c.Label ?? c.Id} » de la pilule ? Il reste dans la carte du groupe.", "customNotch", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    _run(() => _ctx.Editor.SetCellVisible(c.Id, false));
            };
            box.Unchecked += (_, _) => { children.Remove(c.Id); CommitChildren(); };
            boxes.Children.Add(box);
        }
        stack.Children.Add(Row("Enfants", boxes));
        var heads = new[] { ("", "Le pire enfant") }.Concat(children.Select(id => (id, _ctx.Store.Current.Cell(id)?.Label ?? id))).ToArray();
        stack.Children.Add(Row("Tête (anneau de la pilule)", Combo(heads, _cell.Headline ?? "", v => Set(c => { if (v.Length == 0) c.Remove("headline"); else c["headline"] = v; }))));
        stack.Children.Add(Muted("Cocher au moins un enfant fait de cette cellule un groupe : sa source n'est alors plus lue."));
        return stack;

        void CommitChildren() => Set(c =>
        {
            if (children.Count == 0) { c.Remove("children"); c.Remove("headline"); }
            else
            {
                c["children"] = new JsonArray(children.Select(id => (JsonNode)id).ToArray());
                if (c["headline"]?.GetValue<string>() is { } h && !children.Contains(h)) c.Remove("headline");
            }
        });
    }

    // ---- briques (délèguent à Bricks, partagées avec PageBase, avec les largeurs propres à l'éditeur) --------

    private void Set(Action<JsonObject> mutate) => _run(() => _ctx.Editor.SetCell(_cell.Id, mutate));

    private static TextBlock Section(string text) => Bricks.Section(text);
    private static TextBlock Muted(string text) { var t = Ui.Text(text, 11, null, "Muted"); t.TextWrapping = TextWrapping.Wrap; t.VerticalAlignment = VerticalAlignment.Center; return t; }
    private static Grid Row(string label, UIElement field) => Bricks.Row(label, field, 200);
    private static Border Card(UIElement content) => Bricks.Card(content);
    private static ComboBox Combo(IReadOnlyList<(string Value, string Label)> items, string current, Action<string> onChange) => Bricks.Combo(items, current, onChange, 200);
    private static TextBox Debounced(string initial, Action<string> onChange, double width = 360, string? name = null) => Bricks.Debounced(initial, onChange, width, name);
    private static Button Btn(string text, Action click, string style = "Secondary") => Bricks.Btn(text, click, style, 6);
}
