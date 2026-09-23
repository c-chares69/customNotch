using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CustomNotch.App.Settings;

/// <summary>Le message d'une configuration refusée, en tête de l'éditeur : effacé à la prochaine opération réussie,
/// la valeur fautive reste dans son champ pour être corrigée. Stylé comme le Banner de HubWindow.xaml
/// (ClickUp-Extended) : fond Warn, texte sombre — pas de rouge, le message dit ce qui a été refusé, il ne casse
/// rien.</summary>
public sealed class EditorBanner : Border
{
    private static readonly SolidColorBrush DarkText = new(Color.FromRgb(0x1B, 0x1C, 0x24));
    private readonly TextBlock _text = new() { FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = DarkText, TextWrapping = TextWrapping.Wrap };

    public EditorBanner()
    {
        Child = _text;
        Padding = new Thickness(14, 8, 14, 8);
        CornerRadius = new CornerRadius(8);
        Margin = new Thickness(0, 0, 0, 12);
        SetResourceReference(BackgroundProperty, "Warn");
        Visibility = Visibility.Collapsed;
    }

    public void Show(string message) { _text.Text = message; Visibility = Visibility.Visible; }
    public void Clear() => Visibility = Visibility.Collapsed;
}
