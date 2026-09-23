using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using CustomNotch.App.Cells;

namespace CustomNotch.App.Settings;

/// <summary>Les glyphes nommés en grille (le tracé rendu, le nom en infobulle), et un champ pour coller un tracé SVG.</summary>
public static class GlyphGallery
{
    public static UIElement Build(string? current, Action<string?> onPick)
    {
        var stack = new StackPanel();
        var grid = new WrapPanel { MaxWidth = 460 };
        var buttons = new List<ToggleButton>();
        foreach (var name in GlyphLibrary.Names)
        {
            var (data, filled) = GlyphLibrary.Get(name);
            var brush = new System.Windows.Media.SolidColorBrush(StatusPalette.Ink);
            var button = new ToggleButton
            {
                Width = 40, Height = 40, Margin = new Thickness(0, 0, 4, 4), ToolTip = name, Tag = name,
                Content = filled ? Glyphs.Fill(data, 20, brush) : Glyphs.Stroke(data, 20, brush, 1.6),
                IsChecked = string.Equals(name, current, StringComparison.OrdinalIgnoreCase),
            };
            button.SetResourceReference(FrameworkElement.StyleProperty, "GlyphButton");
            button.Checked += (_, _) => { foreach (var other in buttons) if (other != button) other.IsChecked = false; onPick(name); };
            button.Unchecked += (_, _) => { if (buttons.All(b => b.IsChecked != true)) button.IsChecked = true; };
            buttons.Add(button);
            grid.Children.Add(button);
        }
        stack.Children.Add(grid);
        var raw = new TextBox { Width = 460, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 0), Text = current is { } c && c.StartsWith('M') ? c : "" };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        timer.Tick += (_, _) => { timer.Stop(); if (raw.Text.Trim() is { Length: > 0 } t && t.StartsWith('M')) { foreach (var b in buttons) b.IsChecked = false; onPick(t); } };
        raw.TextChanged += (_, _) => { timer.Stop(); timer.Start(); };
        var hint = Ui.Text("Ou un tracé SVG (commence par M) — grille 16×16 pour un trait, 24×24 pour un plein.", 11, null, "Muted");
        stack.Children.Add(raw);
        stack.Children.Add(hint);
        return stack;
    }
}
