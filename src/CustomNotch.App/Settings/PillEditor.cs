using System.Windows;
using System.Windows.Controls;
using CustomNotch.Core.Config;

namespace CustomNotch.App.Settings;

/// <summary>Une pilule : bord, écran, position, échelle, visible. Bord et échelle vont dans le fichier partagé ; écran,
/// position et visibilité sont des choix de poste (surcharge locale), comme un drag.</summary>
public sealed class PillEditor : UserControl
{
    private static readonly (string Value, string Label)[] Edges = { ("right", "Droite"), ("left", "Gauche"), ("top", "Haut"), ("bottom", "Bas") };
    private static readonly (string Value, string Label)[] Shapes = { ("round", "Rondes"), ("square", "Carrées arrondies") };

    public PillEditor(SettingsContext ctx, PillConfig pill, Action<Action> run)
    {
        var body = new StackPanel();
        body.Children.Add(Ui.Text($"Pilule « {pill.Id} »", 17, FontWeights.SemiBold));

        var edges = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (value, label) in Edges)
        {
            var radio = new RadioButton { Content = label, GroupName = "edge-" + pill.Id, IsChecked = pill.Edge == value, Margin = new Thickness(0, 0, 14, 0) };
            radio.Checked += (_, _) => run(() => ctx.Editor.SetPillShared(pill.Id, p => p["edge"] = value));
            edges.Children.Add(radio);
        }

        var screens = new List<(string, string)> { ("", "Écran principal") };
        foreach (var s in System.Windows.Forms.Screen.AllScreens) screens.Add((s.DeviceName, $"{s.DeviceName.TrimStart('\\', '.')} — {s.Bounds.Width}×{s.Bounds.Height}{(s.Primary ? " (principal)" : "")}"));
        var screen = new ComboBox { MinWidth = 260, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var (v, l) in screens) screen.Items.Add(new ComboBoxItem { Content = l, Tag = v });
        screen.SelectedIndex = Math.Max(0, screens.FindIndex(s => s.Item1 == (pill.Screen ?? "")));
        screen.SelectionChanged += (_, _) => { if (screen.SelectedItem is ComboBoxItem it) run(() => ctx.Editor.SetPillLocal(pill.Id, p => { if ((string)it.Tag is { Length: > 0 } v) p["screen"] = v; else p.Remove("screen"); })); };

        var along = Bricks.Slider(pill.Along, 0, 1, 0.01, v => run(() => ctx.Editor.SetPillLocal(pill.Id, p => p["along"] = Math.Round(v, 3))), v => $"{Math.Round(v * 100)} %", "along");
        var scale = Bricks.Slider(pill.Scale, 0.5, 2, 0.1, v => run(() => ctx.Editor.SetPillShared(pill.Id, p => p["scale"] = Math.Round(v, 1))), v => $"×{v:0.0}", "scale");
        var shape = Combo(Shapes, pill.CellsShape, v => run(() => ctx.Editor.SetPillShared(pill.Id, p => { if (v == "round") p.Remove("cellsShape"); else p["cellsShape"] = v; })));
        var visible = new CheckBox { Content = "Visible sur ce poste", IsChecked = pill.Visible };
        visible.Checked += (_, _) => run(() => ctx.Editor.SetPillLocal(pill.Id, p => p["visible"] = true));
        visible.Unchecked += (_, _) => run(() => ctx.Editor.SetPillLocal(pill.Id, p => p["visible"] = false));

        var form = new StackPanel();
        form.Children.Add(Ui.FormRow(Ui.FormLabel("Bord de l'écran"), edges, 200));
        form.Children.Add(Ui.FormRow(Ui.FormLabel("Écran"), screen, 200));
        form.Children.Add(Ui.FormRow(Ui.FormLabel("Position le long du bord"), along, 200));
        form.Children.Add(Ui.FormRow(Ui.FormLabel("Échelle"), scale, 200));
        form.Children.Add(Ui.FormRow(Ui.FormLabel("Forme des cellules"), shape, 200));
        form.Children.Add(Ui.FormRow(Ui.FormLabel(""), visible, 200));
        var card = new Border { Child = form, CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 10, 16, 10), BorderThickness = new Thickness(1), Margin = new Thickness(0, 12, 0, 0) };
        card.SetResourceReference(Border.BackgroundProperty, "Surface");
        card.SetResourceReference(Border.BorderBrushProperty, "Border");
        body.Children.Add(card);
        Content = body;
    }

    private static ComboBox Combo(IReadOnlyList<(string Value, string Label)> items, string current, Action<string> onChange) => Bricks.Combo(items, current, onChange, 200);
}
