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
}
