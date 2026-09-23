using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Button = System.Windows.Controls.Button;

namespace CustomNotch.App.Settings;

/// <summary>Les briques communes à une page (PageBase) et à un éditeur (PillEditor, CellEditor) : carte, ligne de
/// formulaire, combo, case à cocher, champ numérique, champ texte, bouton, indication, message d'erreur — reprises
/// de <c>SettingsPages</c> (ClickUp-Extended), statiques ici (pas d'instance de page à porter) donc chaque brique
/// reçoit son <see cref="FrameworkElement"/> hôte pour <c>FindResource</c>, exactement comme <c>_host</c> là-bas.
/// Un éditeur n'est pas une page (il n'a ni en-tête ni défilement propre), donc ces briques vivent ici plutôt que
/// dans PageBase — les deux les appellent.</summary>
internal static class Bricks
{
    /// <summary>La ligne de formulaire de base : libellé dans une colonne fixe, champ borné à 640 px et calé à
    /// gauche — le gabarit SettingsPages (Ui.FormRow).</summary>
    public static Grid Row(string label, UIElement field, double labelWidth = 240) => Ui.FormRow(Ui.FormLabel(label), field, labelWidth);

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

    // ------------------------------------------------------------------ briques façon SettingsPages (ClickUp-Extended)

    /// <summary>Un bouton discret (Secondary ou GhostButton) qui n'entre dans aucune ligne de formulaire — barre
    /// d'outils de l'arbre pilules/cellules, boutons ↑/↓/✕ d'une liste. <see cref="Action"/> est réservé aux boutons
    /// d'action « pleins » (Primary/Secondary, dans une carte) ; celui-ci garde le style et la marge compacte d'un
    /// bouton d'outils.</summary>
    public static Button Btn(FrameworkElement host, string text, Action click, string style = "Secondary", double rightMargin = 8)
    {
        var b = new Button { Content = text, Margin = new Thickness(0, 0, rightMargin, 0), Style = (Style)host.FindResource(style) };
        b.Click += (_, _) => click();
        return b;
    }

    /// <summary>Un champ texte nu (sans libellé ni ligne de formulaire) qui commit à l'Entrée ou à la perte du
    /// focus — pour composer une ligne à plusieurs champs (seuils avertir/critique, valeur d'une action de carte…).
    /// Comme <see cref="Text"/>, pas d'anti-rebond : la frappe s'applique à la validation, pas à chaque touche.
    /// <paramref name="name"/> (facultatif) pose x:Name pour le focus restauré par PillsPage.ShowEditor().</summary>
    public static TextBox Field(FrameworkElement host, string value, Action<string> commit, double width = 90, string? name = null)
    {
        var box = new TextBox { Text = value, Width = width, Padding = new Thickness(8, 6, 8, 6), Style = (Style)host.FindResource("Field"), HorizontalAlignment = HorizontalAlignment.Left };
        if (name is { Length: > 0 }) box.Name = name;
        var current = value;
        void Commit() { var v = box.Text.Trim(); if (v == current) return; current = v; commit(v); }
        box.LostKeyboardFocus += (_, _) => Commit();
        box.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) Commit(); };
        return box;
    }

    /// <summary>Une indication sous un champ ou en fin de carte, style Hint.</summary>
    public static TextBlock Hint(FrameworkElement host, string text) => new() { Text = text, Style = (Style)host.FindResource("Hint") };

    /// <summary>Le message d'erreur sous un champ fautif : absent (Collapsed) tant qu'il n'y a rien à dire. Couleur
    /// Warn, comme SettingsPages.Problem (ClickUp-Extended) — pas Danger : une valeur refusée qu'on peut corriger
    /// tout de suite n'est pas la même gravité qu'une configuration cassée.</summary>
    public static TextBlock Problem(FrameworkElement host)
        => new() { Style = (Style)host.FindResource("Hint"), Foreground = (Brush)host.FindResource("Warn"), Margin = new Thickness(2, 4, 0, 0), Visibility = Visibility.Collapsed };

    public static void Say(TextBlock problem, string text)
    {
        problem.Text = text;
        problem.Visibility = text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Une case à cocher qui écrit tout de suite au clic ; <paramref name="hint"/> (facultatif) devient
    /// son infobulle.</summary>
    public static CheckBox Check(FrameworkElement host, string label, bool value, Action<bool> commit, string? hint = null)
    {
        var box = new CheckBox { Content = label, IsChecked = value, Margin = new Thickness(0, 7, 0, 5), Foreground = (Brush)host.FindResource("Fg"), ToolTip = hint };
        box.Click += (_, _) => commit(box.IsChecked == true);
        return box;
    }

    /// <summary>Une ligne de formulaire complète : libellé + liste déroulante valeur/libellé, qui rappelle commit
    /// avec la valeur choisie.</summary>
    public static Grid Combo(FrameworkElement host, string label, IReadOnlyList<(string Value, string Text)> options, string? current, Action<string> commit)
    {
        var combo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var (value, text) in options) combo.Items.Add(new ComboBoxItem { Content = text, Tag = value, IsSelected = value == current });
        if (combo.SelectedIndex < 0) combo.SelectedIndex = 0;
        combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is ComboBoxItem it && it.Tag is string v) commit(v); };
        return Row(label, combo);
    }

    /// <summary>Une ligne de formulaire complète : champ numérique 110 px + boutons − / + + suffixe (« px », « h »…),
    /// commit à l'Entrée ou à la perte du focus, bornée à [min, max] et arrondie à <paramref name="decimals"/>.</summary>
    public static Grid Number(FrameworkElement host, string label, double value, double min, double max, Action<double> commit, string suffix = "", int decimals = 0, double step = 1)
    {
        var current = value;
        var box = new TextBox { Width = 110, Padding = new Thickness(8, 6, 8, 6), Text = value.ToString("0.##", CultureInfo.InvariantCulture), Style = (Style)host.FindResource("Field"), HorizontalAlignment = HorizontalAlignment.Left };
        void Commit()
        {
            if (!double.TryParse(box.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) { box.Text = current.ToString("0.##", CultureInfo.InvariantCulture); return; }
            v = Math.Clamp(Math.Round(v, decimals), min, max);
            current = v;
            box.Text = v.ToString("0.##", CultureInfo.InvariantCulture);
            commit(v);
        }
        box.LostKeyboardFocus += (_, _) => Commit();
        box.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) Commit(); };
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(box);
        var minus = new Button { Content = "−", Width = 30, Margin = new Thickness(6, 0, 0, 0), Style = (Style)host.FindResource("Secondary"), Padding = new Thickness(0) };
        var plus = new Button { Content = "+", Width = 30, Margin = new Thickness(3, 0, 0, 0), Style = (Style)host.FindResource("Secondary"), Padding = new Thickness(0) };
        minus.Click += (_, _) => { box.Text = (current - step).ToString("0.##", CultureInfo.InvariantCulture); Commit(); };
        plus.Click += (_, _) => { box.Text = (current + step).ToString("0.##", CultureInfo.InvariantCulture); Commit(); };
        panel.Children.Add(minus);
        panel.Children.Add(plus);
        if (suffix.Length > 0) panel.Children.Add(new TextBlock { Text = suffix, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = (Brush)host.FindResource("Muted") });
        return Row(label, panel);
    }

    /// <summary>Une ligne de formulaire complète : champ texte (ou <see cref="PasswordBox"/> si <paramref name="secret"/>),
    /// commit à l'Entrée ou à la perte du focus — pas d'anti-rebond 300 ms ici, c'est le modèle SettingsPages : la
    /// frappe s'applique à la validation, pas à chaque touche. <paramref name="validate"/> rend le message d'erreur à
    /// afficher sous le champ, ou null si la valeur est acceptée.</summary>
    public static Grid Text(FrameworkElement host, string label, string value, Action<string> commit, string? placeholder = null, bool secret = false, Func<string, string?>? validate = null)
    {
        var panel = new StackPanel();
        var problem = Problem(host);
        var current = value;
        if (secret)
        {
            // Un secret ne s'affiche pas en clair : PasswordBox, même commit.
            var pw = new PasswordBox { Password = value, Padding = new Thickness(10, 7, 10, 7), HorizontalAlignment = HorizontalAlignment.Stretch, ToolTip = placeholder, Background = (Brush)host.FindResource("Bg"), Foreground = (Brush)host.FindResource("Fg"), BorderBrush = (Brush)host.FindResource("Border") };
            void CommitSecret() { var v = pw.Password.Trim(); if (v != current) { current = v; commit(v); } }
            pw.LostKeyboardFocus += (_, _) => CommitSecret();
            pw.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) CommitSecret(); };
            panel.Children.Add(pw);
            panel.Children.Add(problem);
            return Row(label, panel);
        }
        var box = new TextBox { Padding = new Thickness(10, 7, 10, 7), Text = value, Style = (Style)host.FindResource("Field"), HorizontalAlignment = HorizontalAlignment.Stretch, ToolTip = placeholder };
        void Commit()
        {
            var v = box.Text.Trim();
            if (validate is not null)
            {
                var message = validate(v);
                Say(problem, message ?? "");
                if (message is not null) return;
            }
            if (v == current) return;
            current = v;
            commit(v);
        }
        box.LostKeyboardFocus += (_, _) => Commit();
        box.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) Commit(); };
        panel.Children.Add(box);
        panel.Children.Add(problem);
        return Row(label, panel);
    }

    /// <summary>Un bouton, plein (Primary) ou discret (Secondary).</summary>
    public static Button Action(FrameworkElement host, string text, Action click, bool primary = false)
    {
        var button = new Button { Content = text, Style = (Style)host.FindResource(primary ? "Primary" : "Secondary"), Padding = new Thickness(14, 7, 14, 7), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 2) };
        button.Click += (_, _) => click();
        return button;
    }

    /// <summary>Une carte : titre en capitales (style Section) puis le contenu. Une suite de cases à cocher se range
    /// sur deux colonnes à partir de quatre — la carte pleine largeur ne laisse plus une colonne de cases collée à
    /// gauche.</summary>
    public static Border Card(FrameworkElement host, string title, params UIElement[] children)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = title.ToUpperInvariant(), Style = (Style)host.FindResource("Section"), Margin = new Thickness(0, 0, 0, 12) });
        var run = new List<CheckBox>();
        void Flush()
        {
            if (run.Count >= 4)
            {
                var grid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 2, 0, 2) };
                foreach (var box in run) { box.Margin = new Thickness(0, 6, 16, 6); grid.Children.Add(box); }
                stack.Children.Add(grid);
            }
            else foreach (var box in run) stack.Children.Add(box);
            run.Clear();
        }
        foreach (var child in children)
        {
            if (child is CheckBox box) { run.Add(box); continue; }
            Flush();
            stack.Children.Add(child);
        }
        Flush();
        return new Border { Style = (Style)host.FindResource("Card"), Child = stack, HorizontalAlignment = HorizontalAlignment.Stretch };
    }

    public static StackPanel Page(params UIElement[] cards)
    {
        var page = new StackPanel();
        foreach (var card in cards) page.Children.Add(card);
        return page;
    }
}
