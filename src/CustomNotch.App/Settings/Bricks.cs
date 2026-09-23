using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace CustomNotch.App.Settings;

/// <summary>Les briques communes à une page (PageBase) et à un éditeur (PillEditor, CellEditor) : section, carte,
/// ligne de formulaire, combo, champ à anti-rebond, bouton. Un éditeur n'est pas une page (il n'a ni en-tête ni
/// défilement propre), donc ces briques vivent ici plutôt que dans PageBase — les deux les appellent.</summary>
internal static class Bricks
{
    public static TextBlock Section(string text)
    {
        var t = Ui.Text(text.ToUpperInvariant(), 11, FontWeights.SemiBold, "Muted");
        t.Margin = new Thickness(0, 18, 0, 6);
        return t;
    }

    public static Border Card(UIElement content)
    {
        var card = new Border { Child = content, CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 10, 16, 10), BorderThickness = new Thickness(1) };
        card.SetResourceReference(Border.BackgroundProperty, "Surface");
        card.SetResourceReference(Border.BorderBrushProperty, "Border");
        return card;
    }

    public static Grid Row(string label, UIElement field, double labelWidth = 220) => Ui.FormRow(Ui.FormLabel(label), field, labelWidth);

    /// <summary>Une liste déroulante valeur/libellé qui rappelle onChange avec la valeur choisie.</summary>
    public static ComboBox Combo(IReadOnlyList<(string Value, string Label)> items, string? current, Action<string> onChange, double minWidth = 220)
    {
        var combo = new ComboBox { MinWidth = minWidth, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var (value, label) in items) combo.Items.Add(new ComboBoxItem { Content = label, Tag = value });
        combo.SelectedIndex = Math.Max(0, items.ToList().FindIndex(i => i.Value == current));
        combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is ComboBoxItem it && it.Tag is string v) onChange(v); };
        return combo;
    }

    /// <summary>Un champ texte qui rappelle onChange 300 ms après la dernière frappe (et à la perte du focus). Au
    /// démontage, une valeur en attente est validée tout de suite plutôt que perdue ou écrite à l'aveugle après coup
    /// sur un éditeur qui n'existe plus. <paramref name="name"/> (facultatif) pose x:Name : PillsPage.ShowEditor()
    /// s'en sert pour retrouver le même champ dans l'éditeur reconstruit et y rendre le focus.</summary>
    public static TextBox Debounced(string initial, Action<string> onChange, double width = 360, string? name = null)
    {
        var box = new TextBox { Text = initial, Width = width, HorizontalAlignment = HorizontalAlignment.Left };
        if (name is { Length: > 0 }) box.Name = name;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        var last = initial;
        void Flush() { timer.Stop(); if (box.Text != last) { last = box.Text; onChange(box.Text); } }
        timer.Tick += (_, _) => Flush();
        box.TextChanged += (_, _) => { timer.Stop(); timer.Start(); };
        box.LostFocus += (_, _) => Flush();
        box.Unloaded += (_, _) => { if (timer.IsEnabled) Flush(); };
        return box;
    }

    public static Button Btn(string text, Action click, string style = "Secondary", double rightMargin = 8)
    {
        var b = new Button { Content = text, Margin = new Thickness(0, 0, rightMargin, 0) };
        b.SetResourceReference(FrameworkElement.StyleProperty, style);
        b.Click += (_, _) => click();
        return b;
    }

    /// <summary>Un curseur qui n'écrit qu'au relâchement (ou 300 ms après un changement au clavier) : pas une écriture par
    /// pixel. <paramref name="fmt"/> formate la valeur affichée (pourcentage pour une position, « ×1,0 » pour une
    /// échelle, « 120 % » pour une échelle de carte) — un curseur d'échelle à 0,5 ne doit pas afficher « 50 % ». Au
    /// démontage (changement de sélection ou de page pendant les 300 ms), une valeur en attente est validée tout de
    /// suite plutôt que perdue ou écrite à l'aveugle après coup sur un éditeur qui n'existe plus. <paramref name="name"/>
    /// (facultatif) pose x:Name : comme pour CellEditor, PillsPage.ShowEditor() s'en sert pour retrouver le même
    /// curseur dans l'éditeur reconstruit après une écriture (300 ms après la dernière flèche au clavier) et y rendre
    /// le focus — sans ça, les flèches perdent le focus au milieu d'un réglage.</summary>
    public static UIElement Slider(double value, double min, double max, double step, Action<double> commit, Func<double, string> fmt, string? name = null)
    {
        var panel = new DockPanel { Width = 360, HorizontalAlignment = HorizontalAlignment.Left };
        var slider = new System.Windows.Controls.Slider { Minimum = min, Maximum = max, Value = value, TickFrequency = step, IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center };
        if (name is { Length: > 0 }) slider.Name = name;
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
