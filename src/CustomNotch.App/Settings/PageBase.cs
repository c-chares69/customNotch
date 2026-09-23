using System.Windows;
using System.Windows.Controls;

namespace CustomNotch.App.Settings;

/// <summary>Une page : en-tête (titre, sous-titre) et corps vertical, dans un défilement. Les briques communes
/// (section, carte, ligne de formulaire, combo, champ à anti-rebond, bouton) sont dans Bricks, partagées avec les
/// éditeurs ; ici, de simples forwarders pour que les pages existantes appellent Section(...) sans préfixe.</summary>
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

    /// <summary>Briques déléguées à Bricks (partagées avec les éditeurs, qui ne sont pas des pages) : mêmes
    /// signatures et mêmes valeurs par défaut qu'avant l'extraction, pour que les pages existantes n'aient rien à
    /// changer.</summary>
    protected static TextBlock Section(string text) => Bricks.Section(text);

    protected static Border Card(UIElement content) => Bricks.Card(content);

    protected static Grid Row(string label, UIElement field, double labelWidth = 220) => Bricks.Row(label, field, labelWidth);

    protected static ComboBox Combo(IReadOnlyList<(string Value, string Label)> items, string? current, Action<string> onChange) => Bricks.Combo(items, current, onChange);

    protected static TextBox Debounced(string initial, Action<string> onChange, double width = 360) => Bricks.Debounced(initial, onChange, width);

    protected static Button Btn(string text, Action click, string style = "Secondary") => Bricks.Btn(text, click, style);
}
