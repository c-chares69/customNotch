using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using CustomNotch.App.Cells;

namespace CustomNotch.App.Settings;

/// <summary>Les glyphes nommés en grille (le tracé rendu, le nom en infobulle) — avec un premier bouton « Aucun »
/// pour revenir au glyph par défaut de la source — et un champ pour coller un tracé SVG.</summary>
public static class GlyphGallery
{
    public static UIElement Build(string? current, Action<string?> onPick)
    {
        var stack = new StackPanel();
        var grid = new WrapPanel { MaxWidth = 460 };
        var buttons = new List<ToggleButton>();
        // Vrai pendant qu'on recoche/décoche des boutons par code (sélection d'un autre choix, ou tracé SVG collé
        // qui n'en sélectionne aucun) : les gestionnaires Checked/Unchecked se taisent alors, pour qu'une seule
        // bascule programmée ne rappelle pas onPick plusieurs fois ni ne se batte avec le garde-fou anti-décoche.
        var guarding = false;

        void Select(ToggleButton? chosen)
        {
            guarding = true;
            foreach (var b in buttons) b.IsChecked = ReferenceEquals(b, chosen);
            guarding = false;
        }

        ToggleButton MakeButton(string? name, string tooltip, UIElement content, bool initial)
        {
            var button = new ToggleButton
            {
                Width = 40, Height = 40, Margin = new Thickness(0, 0, 4, 4), ToolTip = tooltip, Tag = name,
                Content = content, IsChecked = initial,
            };
            button.SetResourceReference(FrameworkElement.StyleProperty, "GlyphButton");
            button.Checked += (_, _) => { if (guarding) return; Select(button); onPick(name); };
            // Cliquer sur le bouton déjà choisi le décocherait sans rien sélectionner d'autre : on le recoche tout
            // de suite (sans relancer onPick, qui a déjà la bonne valeur) plutôt que de laisser la grille sans
            // aucun choix visible.
            button.Unchecked += (_, _) => { if (guarding) return; guarding = true; button.IsChecked = true; guarding = false; };
            buttons.Add(button);
            return button;
        }

        var (noneData, noneFilled) = GlyphLibrary.Get(null);
        var noneBrush = new SolidColorBrush(StatusPalette.Ink);
        var none = MakeButton(null, "Glyph par défaut de la source", noneFilled ? Glyphs.Fill(noneData, 20, noneBrush) : Glyphs.Stroke(noneData, 20, noneBrush, 1.6), current is null);
        grid.Children.Add(none);
        foreach (var name in GlyphLibrary.Names)
        {
            var (data, filled) = GlyphLibrary.Get(name);
            var brush = new SolidColorBrush(StatusPalette.Ink);
            var content = filled ? Glyphs.Fill(data, 20, brush) : Glyphs.Stroke(data, 20, brush, 1.6);
            var button = MakeButton(name, name, content, string.Equals(name, current, StringComparison.OrdinalIgnoreCase));
            grid.Children.Add(button);
        }
        stack.Children.Add(grid);
        var raw = new TextBox { Width = 460, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 0), Text = current is { } c && c.StartsWith('M') ? c : "" };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var t = raw.Text.Trim();
            if (t.Length == 0) { Select(none); onPick(null); }
            else if (t.StartsWith('M')) { Select(null); onPick(t); }
        };
        raw.TextChanged += (_, _) => { timer.Stop(); timer.Start(); };
        var hint = Ui.Text("Ou un tracé SVG (commence par M) — grille 16×16 pour un trait, 24×24 pour un plein.", 11, null, "Muted");
        stack.Children.Add(raw);
        stack.Children.Add(hint);
        return stack;
    }
}
