using System.Windows;
using System.Windows.Controls;
using CustomNotch.Core.Config;

namespace CustomNotch.App.Settings;

/// <summary>Une pilule : bord, écran, position, échelle, visible. Bord et échelle vont dans le fichier partagé ; écran,
/// position et visibilité sont des choix de poste (surcharge locale), comme un drag.</summary>
public sealed class PillEditor : UserControl
{
    private static readonly (string Value, string Label)[] Shapes = { ("round", "Rondes"), ("square", "Carrées arrondies") };
    private static readonly (string Value, string Label)[] Edges = { ("right", "Droite"), ("left", "Gauche"), ("top", "Haut"), ("bottom", "Bas") };

    public PillEditor(SettingsContext ctx, PillConfig pill, Action<Action> run)
    {
        // Le bord se choisit par radios (pas une liste déroulante) : les quatre valeurs se comparent d'un coup d'œil.
        var edges = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (value, label) in Edges)
        {
            var radio = new RadioButton { Content = label, GroupName = "edge-" + pill.Id, IsChecked = pill.Edge == value, Margin = new Thickness(0, 0, 14, 0) };
            radio.Checked += (_, _) => run(() => ctx.Editor.SetPillShared(pill.Id, p => p["edge"] = value));
            edges.Children.Add(radio);
        }

        var screens = new List<(string Value, string Text)> { ("", "Écran principal") };
        foreach (var s in System.Windows.Forms.Screen.AllScreens) screens.Add((s.DeviceName, $"{s.DeviceName.TrimStart('\\', '.')} — {s.Bounds.Width}×{s.Bounds.Height}{(s.Primary ? " (principal)" : "")}"));

        var along = Bricks.Slider(pill.Along, 0, 1, 0.01, v => run(() => ctx.Editor.SetPillLocal(pill.Id, p => p["along"] = Math.Round(v, 3))), v => $"{Math.Round(v * 100)} %", "along");
        var scale = Bricks.Slider(pill.Scale, 0.5, 2, 0.1, v => run(() => ctx.Editor.SetPillShared(pill.Id, p => p["scale"] = Math.Round(v, 1))), v => $"×{v:0.0}", "scale");
        var visible = Bricks.Check(this, "Visible sur ce poste", pill.Visible, v => run(() => ctx.Editor.SetPillLocal(pill.Id, p => p["visible"] = v)));

        Content = Bricks.Card(this, $"Pilule « {pill.Id} »",
            Bricks.Row("Bord de l'écran", edges),
            Bricks.Combo(this, "Écran", screens, pill.Screen ?? "", v => run(() => ctx.Editor.SetPillLocal(pill.Id, p => { if (v.Length > 0) p["screen"] = v; else p.Remove("screen"); }))),
            Bricks.Row("Position le long du bord", along),
            Bricks.Row("Échelle", scale),
            Bricks.Combo(this, "Forme des cellules", Shapes, pill.CellsShape, v => run(() => ctx.Editor.SetPillShared(pill.Id, p => { if (v == "round") p.Remove("cellsShape"); else p["cellsShape"] = v; }))),
            visible);
    }
}
