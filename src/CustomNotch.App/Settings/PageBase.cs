using System.Windows;
using System.Windows.Controls;
using CustomNotch.Core.Config;

namespace CustomNotch.App.Settings;

/// <summary>Une page : en-tête (titre, sous-titre) et corps vertical, dans un défilement. Les briques communes
/// (section, carte, ligne de formulaire, combo, champ à anti-rebond, bouton) sont dans Bricks, partagées avec les
/// éditeurs ; ici, de simples forwarders pour que les pages existantes appellent Section(...) sans préfixe.</summary>
public abstract class PageBase : UserControl
{
    protected readonly StackPanel Body = new();
    /// <summary>Le bandeau de refus de configuration, commun à toute page qui écrit via ConfigEditor. Chaque page
    /// l'insère où elle veut dans sa propre mise en page (en tête pour GeneralPage, à côté de l'éditeur pour
    /// PillsPage) ; c'est Try() ci-dessous qui la remplit et la vide.</summary>
    protected readonly EditorBanner Banner = new();
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

    /// <summary>Note un désabonnement générique à exécuter par Detach() - pour une page qui s'abonne à autre
    /// chose que ConfigStore (ClaudePage à ClaudeSource.StatusChanged, par exemple).</summary>
    protected void OnDetach(Action undo) => _detach.Add(undo);

    public void Detach()
    {
        foreach (var undo in _detach) undo();
        _detach.Clear();
    }

    /// <summary>Exécute une opération de l'éditeur ; un refus s'affiche dans Banner, une réussite l'efface.</summary>
    protected void Try(Action op)
    {
        try { op(); Banner.Clear(); }
        catch (ConfigException ex) { Banner.Show(ex.Message); }
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
