using System.Windows;
using System.Windows.Controls;

namespace CustomNotch.App.Settings;

/// <summary>Le message d'une configuration refusée, en tête de l'éditeur : rouge, effacé à la prochaine opération réussie.
/// La valeur fautive reste dans son champ pour être corrigée.</summary>
public sealed class EditorBanner : Border
{
    private readonly TextBlock _text = Ui.Text("", 12, FontWeights.SemiBold, "Danger");

    public EditorBanner()
    {
        Child = _text;
        _text.TextWrapping = TextWrapping.Wrap;
        Padding = new Thickness(12, 8, 12, 8);
        CornerRadius = new CornerRadius(8);
        Margin = new Thickness(0, 0, 0, 12);
        BorderThickness = new Thickness(1);
        SetResourceReference(BorderBrushProperty, "Danger");
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(28, 240, 101, 94));
        Visibility = Visibility.Collapsed;
    }

    public void Show(string message) { _text.Text = message; Visibility = Visibility.Visible; }
    public void Clear() => Visibility = Visibility.Collapsed;
}
