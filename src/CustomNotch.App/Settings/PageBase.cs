using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace CustomNotch.App.Settings;

/// <summary>Une page : en-tête (titre, sous-titre) et corps vertical, dans un défilement. Les briques communes aux pages
/// (section, carte, ligne de formulaire, combo, champ à anti-rebond) vivent ici pour que chaque page reste courte.</summary>
public abstract class PageBase : UserControl
{
    protected readonly StackPanel Body = new();
    private readonly List<Action> _detach = new();

    protected PageBase(SettingsContext ctx, string title, string subtitle = "")
    {
        Ctx = ctx;
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
        header.Children.Add(Ui.Text(title, 21, FontWeights.SemiBold));
        if (subtitle.Length > 0)
        {
            var sub = Ui.Text(subtitle, 12, null, "Muted");
            sub.TextWrapping = TextWrapping.Wrap;
            sub.Margin = new Thickness(0, 4, 0, 0);
            header.Children.Add(sub);
        }
        var stack = new StackPanel { Margin = new Thickness(28, 22, 28, 20) };
        stack.Children.Add(header);
        stack.Children.Add(Body);
        Content = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false };
    }

    protected SettingsContext Ctx { get; }

    /// <summary>Relit l'état et reconstruit ce qui doit l'être (appelé à l'ouverture et quand la config change ailleurs).</summary>
    public virtual void Refresh() { }

    /// <summary>Abonne un gestionnaire au ConfigStore en notant comment le désabonner.</summary>
    protected void OnStoreChanged(Action handler)
    {
        Action<Core.Config.CellsFile> h = _ => Dispatcher.BeginInvoke(handler);
        Ctx.Store.Changed += h;
        _detach.Add(() => Ctx.Store.Changed -= h);
    }

    public void Detach()
    {
        foreach (var undo in _detach) undo();
        _detach.Clear();
    }

    protected static TextBlock Section(string text)
    {
        var t = Ui.Text(text.ToUpperInvariant(), 11, FontWeights.SemiBold, "Muted");
        t.Margin = new Thickness(0, 18, 0, 6);
        return t;
    }

    protected static Border Card(UIElement content)
    {
        var card = new Border { Child = content, CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 10, 16, 10), BorderThickness = new Thickness(1) };
        card.SetResourceReference(Border.BackgroundProperty, "Surface");
        card.SetResourceReference(Border.BorderBrushProperty, "Border");
        return card;
    }

    protected static Grid Row(string label, UIElement field, double labelWidth = 220) => Ui.FormRow(Ui.FormLabel(label), field, labelWidth);

    /// <summary>Une liste déroulante valeur/libellé qui rappelle onChange avec la valeur choisie.</summary>
    protected static ComboBox Combo(IReadOnlyList<(string Value, string Label)> items, string? current, Action<string> onChange)
    {
        var combo = new ComboBox { MinWidth = 220, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (var (value, label) in items) combo.Items.Add(new ComboBoxItem { Content = label, Tag = value });
        combo.SelectedIndex = Math.Max(0, items.ToList().FindIndex(i => i.Value == current));
        combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is ComboBoxItem it && it.Tag is string v) onChange(v); };
        return combo;
    }

    /// <summary>Un champ texte qui rappelle onChange 300 ms après la dernière frappe (et à la perte du focus).</summary>
    protected static TextBox Debounced(string initial, Action<string> onChange, double width = 360)
    {
        var box = new TextBox { Text = initial, Width = width, HorizontalAlignment = HorizontalAlignment.Left };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        var last = initial;
        void Flush() { timer.Stop(); if (box.Text != last) { last = box.Text; onChange(box.Text); } }
        timer.Tick += (_, _) => Flush();
        box.TextChanged += (_, _) => { timer.Stop(); timer.Start(); };
        box.LostFocus += (_, _) => Flush();
        return box;
    }

    protected static Button Btn(string text, Action click, string style = "Secondary")
    {
        var b = new Button { Content = text, Margin = new Thickness(0, 0, 8, 0) };
        b.SetResourceReference(StyleProperty, style);
        b.Click += (_, _) => click();
        return b;
    }
}
