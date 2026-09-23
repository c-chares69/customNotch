using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CustomNotch.Core.Sources;

namespace CustomNotch.App.Settings;

/// <summary>Le formulaire d'une source, construit depuis son schéma : une rangée par champ, groupées par section. Un
/// champ « secret » n'affiche jamais sa valeur ; il dit « défini » et sait s'effacer. La page décide où chaque
/// changement va (params ou secrets.json) : le formulaire ne fait que rappeler. Rendu avec les briques (ligne de
/// formulaire, champ stylé « Field », message d'erreur « Problem ») sans porter de <see cref="FrameworkElement"/>
/// hôte propre : Build garde ses quatre paramètres (SchemaFormTests, CellEditor l'appellent déjà comme ça) et pose
/// les styles par <c>SetResourceReference</c> — comme <see cref="Ui"/> — plutôt que par <c>host.FindResource</c>.</summary>
public static class SchemaForm
{
    public enum Kind { Text, Number, Check, Choice, Path, Url, Secret }

    public static Kind ControlKind(SchemaField field) => field.Type switch
    {
        "number" => Kind.Number, "bool" => Kind.Check, "choice" => Kind.Choice, "path" => Kind.Path, "url" => Kind.Url, "secret" => Kind.Secret, _ => Kind.Text,
    };

    public static string InitialText(SchemaField field, JsonObject? parameters)
    {
        var node = parameters?[field.Name];
        if (node is JsonValue v)
        {
            if (v.TryGetValue<string>(out var s)) return s.StartsWith("${secret:", StringComparison.Ordinal) ? "" : s;
            if (v.TryGetValue<double>(out var d)) return d.ToString(CultureInfo.InvariantCulture);
            if (v.TryGetValue<bool>(out var b)) return b ? "true" : "false";
        }
        return field.Default ?? "";
    }

    public static bool IsSecretSet(SchemaField field, JsonObject? parameters)
        => parameters?[field.Name] is JsonValue v && v.TryGetValue<string>(out var s) && s.StartsWith("${secret:", StringComparison.Ordinal);

    public static double? ParseNumber(string text)
        => double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;

    public static UIElement Build(SourceSchema schema, JsonObject? parameters, Action<string, JsonNode?> onChange, Action<string, string?> onSecret)
    {
        var stack = new StackPanel();
        if (schema.Fields.Count == 0)
        {
            stack.Children.Add(Ui.Text("Cette source n'a pas de paramètre.", 12, null, "Muted"));
            return stack;
        }
        foreach (var group in schema.Fields.GroupBy(f => f.Group ?? ""))
        {
            if (group.Key.Length > 0)
            {
                var title = Ui.Text(group.Key.ToUpperInvariant(), 11, FontWeights.SemiBold, "Muted");
                title.Margin = new Thickness(0, 10, 0, 2);
                stack.Children.Add(title);
            }
            foreach (var field in group)
            {
                var label = field.Label + (field.Required ? " *" : "");
                var row = Ui.FormRow(Ui.FormLabel(label), Control(field, parameters, onChange, onSecret), 200);
                if (field.Help is { } help)
                {
                    var wrap = new StackPanel();
                    wrap.Children.Add(row);
                    var h = Ui.Text(help, 11, null, "Muted"); h.Margin = new Thickness(200, -4, 0, 6); h.TextWrapping = TextWrapping.Wrap;
                    wrap.Children.Add(h);
                    stack.Children.Add(wrap);
                }
                else stack.Children.Add(row);
            }
        }
        return stack;
    }

    private static UIElement Control(SchemaField field, JsonObject? parameters, Action<string, JsonNode?> onChange, Action<string, string?> onSecret)
    {
        switch (ControlKind(field))
        {
            case Kind.Check:
                var check = new CheckBox { IsChecked = InitialText(field, parameters) == "true", VerticalAlignment = VerticalAlignment.Center };
                check.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "Fg");
                check.Checked += (_, _) => onChange(field.Name, JsonValue.Create(true));
                check.Unchecked += (_, _) => onChange(field.Name, JsonValue.Create(false));
                return check;
            case Kind.Choice:
                var combo = new ComboBox { MinWidth = 200, HorizontalAlignment = HorizontalAlignment.Left };
                var choices = field.Choices ?? Array.Empty<string>();
                foreach (var c in choices) combo.Items.Add(c);
                combo.SelectedIndex = Math.Max(0, choices.ToList().IndexOf(InitialText(field, parameters)));
                combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is string v) onChange(field.Name, JsonValue.Create(v)); };
                return combo;
            case Kind.Secret:
                var panel = new StackPanel { Orientation = Orientation.Horizontal };
                var pass = new PasswordBox { Width = 260 };
                var state = Ui.Text(IsSecretSet(field, parameters) ? "défini" : "non défini", 11, null, "Muted");
                state.Margin = new Thickness(10, 0, 10, 0); state.VerticalAlignment = VerticalAlignment.Center;
                var save = new Button { Content = "Enregistrer" }; save.SetResourceReference(FrameworkElement.StyleProperty, "Secondary");
                save.Click += (_, _) => { if (pass.Password.Length == 0) return; onSecret(field.Name, pass.Password); pass.Clear(); state.Text = "défini"; };
                var clear = new Button { Content = "Effacer", Margin = new Thickness(6, 0, 0, 0) }; clear.SetResourceReference(FrameworkElement.StyleProperty, "GhostButton");
                clear.Click += (_, _) => { onSecret(field.Name, null); state.Text = "non défini"; };
                panel.Children.Add(pass); panel.Children.Add(state); panel.Children.Add(save); panel.Children.Add(clear);
                return panel;
            case Kind.Path:
                var pathPanel = new StackPanel { Orientation = Orientation.Horizontal };
                var pathField = Text(field, parameters, s => onChange(field.Name, s.Length == 0 ? null : JsonValue.Create(s)));
                var browse = new Button { Content = "…", Margin = new Thickness(6, 0, 0, 0), MinWidth = 32, VerticalAlignment = VerticalAlignment.Top }; browse.SetResourceReference(FrameworkElement.StyleProperty, "Secondary");
                browse.Click += (_, _) => { var d = new Microsoft.Win32.OpenFileDialog { CheckFileExists = false }; if (d.ShowDialog() == true) pathField.Box.Text = d.FileName; };
                pathPanel.Children.Add(pathField.Panel); pathPanel.Children.Add(browse);
                return pathPanel;
            case Kind.Number:
                return Text(field, parameters, s =>
                {
                    if (s.Trim().Length == 0) { onChange(field.Name, null); return; }
                    if (ParseNumber(s) is { } n) onChange(field.Name, JsonValue.Create(n));
                }).Panel;
            default:
                return Text(field, parameters, s => onChange(field.Name, s.Length == 0 ? null : JsonValue.Create(s))).Panel;
        }
    }

    /// <summary>Un champ texte stylé « Field », qui commit à l'Entrée ou à la perte du focus (pas d'anti-rebond,
    /// comme <see cref="Bricks.Text"/>) — accompagné de son message d'erreur « Problem », affiché tant qu'un
    /// nombre attendu n'en est pas un. Nommé d'après le champ (x:Name) : PillsPage.ShowEditor() s'en sert pour
    /// retrouver ce même contrôle dans l'éditeur reconstruit après une écriture et y rendre le focus.</summary>
    private static (StackPanel Panel, TextBox Box) Text(SchemaField field, JsonObject? parameters, Action<string> commit)
    {
        var box = new TextBox { Text = InitialText(field, parameters), Width = 340, HorizontalAlignment = HorizontalAlignment.Left, Name = FieldName(field) };
        box.SetResourceReference(System.Windows.Controls.Control.StyleProperty, "Field");
        var problem = new TextBlock { Margin = new Thickness(2, 4, 0, 0), Visibility = Visibility.Collapsed };
        problem.SetResourceReference(TextBlock.StyleProperty, "Hint");
        problem.SetResourceReference(TextBlock.ForegroundProperty, "Warn");
        var current = box.Text;
        void Commit()
        {
            if (box.Text == current) return;
            if (ControlKind(field) == Kind.Number && box.Text.Trim().Length > 0 && ParseNumber(box.Text) is null)
            {
                problem.Text = $"« {box.Text.Trim()} » n'est pas un nombre.";
                problem.Visibility = Visibility.Visible;
                return;
            }
            problem.Visibility = Visibility.Collapsed;
            current = box.Text;
            commit(box.Text);
        }
        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) Commit(); };
        var panel = new StackPanel();
        panel.Children.Add(box);
        panel.Children.Add(problem);
        return (panel, box);
    }

    /// <summary>« param_ » + le nom du champ, assaini en nom WPF valide (lettres, chiffres, tiret bas) — un nom de
    /// champ de source pourrait contenir un caractère que x:Name refuse.</summary>
    private static string FieldName(SchemaField field)
    {
        var sb = new System.Text.StringBuilder("param_");
        foreach (var ch in field.Name) sb.Append(ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' ? ch : '_');
        return sb.ToString();
    }
}
