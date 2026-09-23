using System.Windows;
using System.Windows.Controls;
using CustomNotch.Core.Config;

namespace CustomNotch.App.Settings;

/// <summary>Une page : un corps vertical de cartes (Body), sans en-tête ni défilement propres — la coquille
/// (SettingsWindow) les dessine désormais, comme HubWindow.xaml dessine PageTitle/PageSubtitle/PageScroll une
/// seule fois pour toutes les pages plutôt que chacune la sienne. Title/Subtitle restent portés par la page (posés
/// une fois au constructeur) : la fenêtre les lit à chaque bascule de page (Show(key)). Les briques (carte, ligne
/// de formulaire, combo, champ, bouton…) sont dans Bricks, partagées avec les éditeurs (PillEditor, CellEditor) qui
/// ne sont pas des pages ; chaque page les appelle directement (Bricks.Card(this, …), Bricks.Row(…)…).</summary>
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
        Title = title;
        Subtitle = subtitle;
        Content = Body;
    }

    protected SettingsContext Ctx { get; }

    /// <summary>Le titre de l'en-tête (22 semi-gras) et son sous-titre (Hint) — dessinés par SettingsWindow, pas
    /// ici : une page n'a plus de ScrollViewer ni de marge à elle, c'est la fenêtre qui les pose (36,12,18,24),
    /// comme HubWindow.xaml.</summary>
    public string Title { get; }

    public string Subtitle { get; }

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

}
