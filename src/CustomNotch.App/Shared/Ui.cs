using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using Ellipse = System.Windows.Shapes.Ellipse;

namespace CustomNotch.App;

/// <summary>Les briques communes au tableau de bord, au volet et aux pages : une seule
/// façon de dessiner une pastille, un bouton lecture / pause, un bouton-icône, une ligne.
/// Tout ce qui est coloré passe par les pinceaux du thème (DynamicResource).</summary>
internal static class Ui
{
    public static TextBlock Text(string text, double size = 13, FontWeight? weight = null, string brush = "Fg")
    {
        var block = new TextBlock { Text = text, FontSize = size, FontWeight = weight ?? FontWeights.Normal, TextTrimming = TextTrimming.CharacterEllipsis };
        block.SetResourceReference(TextBlock.ForegroundProperty, brush);
        return block;
    }

    /// <summary>Une pastille : un point coloré (facultatif) et un libellé court, sur une surface discrète.</summary>
    public static Border Pill(string label, Color? dot, string brush = "Muted", string? tip = null, Action? click = null)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        if (dot is { } c) panel.Children.Add(new Ellipse { Width = 7, Height = 7, Fill = new SolidColorBrush(c), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0) });
        panel.Children.Add(Text(label, 11, FontWeights.SemiBold, brush));
        var pill = new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(9, 3, 9, 3), VerticalAlignment = VerticalAlignment.Center, Child = panel, ToolTip = tip, Cursor = click is null ? Cursors.Arrow : Cursors.Hand };
        pill.SetResourceReference(Border.BackgroundProperty, "SurfaceHover");
        if (click is not null) pill.MouseLeftButtonUp += (_, e) => { click(); e.Handled = true; };
        return pill;
    }

    /// <summary>La même pastille, le point pris dans un pinceau du thème (« Success », « Accent »…).</summary>
    public static Border Pill(FrameworkElement host, string label, string? dotBrush, string brush = "Muted", string? tip = null, Action? click = null)
        => Pill(label, dotBrush is null ? null : (host.FindResource(dotBrush) as SolidColorBrush)?.Color, brush, tip, click);

    /// <summary>Le bouton lecture / pause carré du tableau de bord : plein (accent) quand ça tourne.</summary>
    public static Button PlayButton(FrameworkElement host, bool running, double size, string tip, Action click)
    {
        var button = new Button
        {
            Content = Glyphs.Icon(running, size * 0.32, (Brush)host.FindResource(running ? "OnAccent" : "Muted")), Width = size, Height = size, Padding = new Thickness(0),
            Style = (Style)host.FindResource(running ? "Primary" : "Secondary"), VerticalAlignment = VerticalAlignment.Center, ToolTip = tip,
        };
        button.Click += (_, _) => click();
        return button;
    }

    /// <summary>Un bouton-icône discret : un tracé, une infobulle, une surface au survol.</summary>
    public static Button IconButton(FrameworkElement host, Geometry glyph, string tip, Action click, double size = 30, string brush = "Muted")
    {
        var button = new Button { Content = Glyphs.Stroke(glyph, size * 0.5, brush), Width = size, Height = size - 2, Padding = new Thickness(0), Style = (Style)host.FindResource("GhostButton"), ToolTip = tip };
        button.Click += (_, _) => click();
        return button;
    }

    /// <summary>Une icône dans un disque teinté : le tracé dans la couleur du ton (Accent,
    /// Success, Warn, Danger, Muted…), sur ce même ton très dilué - ce qui fait reconnaître
    /// une notification d'un coup d'œil. <paramref name="filled"/> pour les glyphes pleins
    /// (lecture, pause), tracés plus petits pour peser autant qu'un trait.</summary>
    public static Border IconChip(FrameworkElement host, Geometry glyph, string tone, double size = 34, bool filled = false)
    {
        var color = (host.FindResource(tone) as SolidColorBrush)?.Color ?? Colors.Gray;
        var chip = new Border
        {
            Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), VerticalAlignment = VerticalAlignment.Top,
            Background = new SolidColorBrush(Theme.WithAlpha(color, (byte)(Theme.CurrentPalette == Theme.Dark ? 48 : 32))),
            Child = filled ? Glyphs.Fill(glyph, size * 0.33, (Brush)host.FindResource(tone)) : Glyphs.Stroke(glyph, size * 0.5, tone, 1.6),
        };
        return chip;
    }

    /// <summary>Une ligne de liste : surface au survol, coins arrondis, menu contextuel facultatif.</summary>
    public static Border Row(UIElement content, Thickness padding, Action? rightClick = null)
    {
        var row = new Border { Child = content, Padding = padding, CornerRadius = new CornerRadius(8), Background = Brushes.Transparent };
        row.MouseEnter += (_, _) => row.SetResourceReference(Border.BackgroundProperty, "SurfaceHover");
        row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
        if (rightClick is not null) row.MouseRightButtonUp += (_, e) => { rightClick(); e.Handled = true; };
        return row;
    }

    /// <summary>Une ligne de formulaire : le libellé dans une colonne fixe, le champ dans une
    /// colonne bornée à 640 px et calée à gauche - un champ étiré sous une largeur maximale se
    /// centrerait sinon dans l'espace restant, et flotterait loin de son libellé sur un grand écran.</summary>
    public static Grid FormRow(UIElement label, UIElement field, double labelWidth = 240)
    {
        var row = new Grid { Margin = new Thickness(0, 6, 0, 6) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(labelWidth) });
        // La borne est sur la colonne, pas sur le champ : la grille garde l'espace restant à droite.
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 640 });
        Grid.SetColumn(label, 0);
        Grid.SetColumn(field, 1);
        row.Children.Add(label);
        row.Children.Add(field);
        return row;
    }

    /// <summary>Le libellé d'une ligne de formulaire.</summary>
    public static TextBlock FormLabel(string text)
    {
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 16, 0) };
        label.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        return label;
    }

    public static string Relative(long ms)
    {
        var seconds = Math.Max(0, ms / 1000);
        if (seconds < 60) return "à l'instant";
        if (seconds < 3600) return $"il y a {seconds / 60} min";
        if (seconds < 86400) return $"il y a {seconds / 3600} h";
        return $"il y a {seconds / 86400} j";
    }
}
