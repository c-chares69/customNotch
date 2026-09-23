using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CustomNotch.Core.Config;

namespace CustomNotch.App.Settings;

/// <summary>Une pilule : bord, écran, position, échelle, visible. Bord et échelle vont dans le fichier partagé ; écran,
/// position et visibilité sont des choix de poste (surcharge locale), comme un drag.</summary>
public sealed class PillEditor : UserControl
{
    private static readonly (string Value, string Label)[] Edges = { ("right", "Droite"), ("left", "Gauche"), ("top", "Haut"), ("bottom", "Bas") };

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

        var along = Slider(pill.Along, 0, 1, 0.01, v => run(() => ctx.Editor.SetPillLocal(pill.Id, p => p["along"] = Math.Round(v, 3))), v => $"{Math.Round(v * 100)} %");
        var scale = Slider(pill.Scale, 0.5, 2, 0.1, v => run(() => ctx.Editor.SetPillShared(pill.Id, p => p["scale"] = Math.Round(v, 1))), v => $"×{v:0.0}");
        var visible = new CheckBox { Content = "Visible sur ce poste", IsChecked = pill.Visible };
        visible.Checked += (_, _) => run(() => ctx.Editor.SetPillLocal(pill.Id, p => p["visible"] = true));
        visible.Unchecked += (_, _) => run(() => ctx.Editor.SetPillLocal(pill.Id, p => p["visible"] = false));

        var form = new StackPanel();
        form.Children.Add(Ui.FormRow(Ui.FormLabel("Bord de l'écran"), edges, 200));
        form.Children.Add(Ui.FormRow(Ui.FormLabel("Écran"), screen, 200));
        form.Children.Add(Ui.FormRow(Ui.FormLabel("Position le long du bord"), along, 200));
        form.Children.Add(Ui.FormRow(Ui.FormLabel("Échelle"), scale, 200));
        form.Children.Add(Ui.FormRow(Ui.FormLabel(""), visible, 200));
        var card = new Border { Child = form, CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 10, 16, 10), BorderThickness = new Thickness(1), Margin = new Thickness(0, 12, 0, 0) };
        card.SetResourceReference(Border.BackgroundProperty, "Surface");
        card.SetResourceReference(Border.BorderBrushProperty, "Border");
        body.Children.Add(card);
        Content = body;
    }

    /// <summary>Un curseur qui n'écrit qu'au relâchement (ou 300 ms après un changement au clavier) : pas une écriture par
    /// pixel. <paramref name="fmt"/> formate la valeur affichée (pourcentage pour une position, « ×1,0 » pour une
    /// échelle) — un curseur d'échelle à 0,5 ne doit pas afficher « 50 % ». Au démontage (changement de sélection ou de
    /// page pendant les 300 ms), une valeur en attente est validée tout de suite plutôt que perdue ou écrite à l'aveugle
    /// après coup sur un éditeur qui n'existe plus.</summary>
    private static UIElement Slider(double value, double min, double max, double step, Action<double> commit, Func<double, string> fmt)
    {
        var panel = new DockPanel { Width = 360, HorizontalAlignment = HorizontalAlignment.Left };
        var slider = new System.Windows.Controls.Slider { Minimum = min, Maximum = max, Value = value, TickFrequency = step, IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center };
        var label = Ui.Text(fmt(value), 12, null, "Muted"); label.Width = 56; label.Margin = new Thickness(10, 0, 0, 0); label.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(label, Dock.Right);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        var last = value;
        void Flush() { timer.Stop(); if (Math.Abs(slider.Value - last) > 1e-9) { last = slider.Value; commit(slider.Value); } }
        timer.Tick += (_, _) => Flush();
        slider.ValueChanged += (_, e) => { label.Text = fmt(e.NewValue); timer.Stop(); timer.Start(); };
        slider.Unloaded += (_, _) => { if (timer.IsEnabled) Flush(); };
        panel.Children.Add(label);
        panel.Children.Add(slider);
        return panel;
    }
}
