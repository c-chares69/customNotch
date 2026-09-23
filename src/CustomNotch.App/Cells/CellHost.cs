using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CustomNotch.App.Notch;
using CustomNotch.Core.Config;
using CustomNotch.Core.Model;

namespace CustomNotch.App.Cells;

/// <summary>Une cellule dans la pilule : la face (anneau, valeur, statut, sparkline) et la légende dessous. Change de
/// face si le type déduit change. Pression : la face se rétracte à 93 % puis revient (codenotch .pressed).</summary>
public sealed class CellHost : StackPanel
{
    public event Action<CellHost>? Hovered;
    public event Action<CellHost>? Unhovered;
    public event Action<CellHost>? Clicked;
    public event Action<CellHost>? RightClicked;

    private readonly PillMetrics _m;
    private readonly Grid _faceSlot = new() { RenderTransformOrigin = new Point(0.5, 0.5) };
    private readonly ScaleTransform _press = new(1, 1);
    private readonly TextBlock _caption = new() { FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center };
    private CellFace? _face;
    private CellKind? _kind;

    public CellHost(CellConfig cell, PillMetrics m)
    {
        Cell = cell;
        _m = m;
        Orientation = Orientation.Vertical;
        HorizontalAlignment = HorizontalAlignment.Center;
        Margin = new Thickness(0, 0, 0, m.Gap);
        Width = m.Width;
        _caption.FontSize = 15 * m.Scale;
        _caption.Height = m.Caption;
        _caption.Margin = new Thickness(0, 6 * m.Scale - 6, 0, 0);
        TextOptions.SetTextFormattingMode(_caption, TextFormattingMode.Display);
        _caption.SetValue(Typography.NumeralAlignmentProperty, FontNumeralAlignment.Tabular);
        _faceSlot.RenderTransform = _press;
        _faceSlot.Width = m.Ring; _faceSlot.Height = m.Ring;
        _faceSlot.HorizontalAlignment = HorizontalAlignment.Center;
        _faceSlot.Background = Brushes.Transparent;   // pour recevoir la souris sur toute la face
        Children.Add(_faceSlot);
        Children.Add(_caption);
        Background = Brushes.Transparent;
        Cursor = Cursors.Hand;
        MouseEnter += (_, _) => Hovered?.Invoke(this);
        MouseLeave += (_, _) => Unhovered?.Invoke(this);
        MouseLeftButtonDown += (_, e) => { Press(0.93); e.Handled = true; };
        MouseLeftButtonUp += (_, e) => { Press(1); Clicked?.Invoke(this); e.Handled = true; };
        MouseRightButtonUp += (_, e) => { RightClicked?.Invoke(this); e.Handled = true; };
    }

    public CellConfig Cell { get; private set; }
    public CellView? Last { get; private set; }
    /// <summary>L'échelle des métriques figées à la construction : PillWindow.RebuildCells s'en sert pour
    /// détecter qu'un CellHost existant est périmé (Pill.Scale a changé) et doit être reconstruit.</summary>
    public double Scale => _m.Scale;

    public void Rebind(CellConfig cell) => Cell = cell;

    /// <summary>Pour un bord horizontal : la cellule ne change pas, seule sa marge passe à droite.</summary>
    public void SetHorizontal(bool horizontal) => Margin = horizontal ? new Thickness(0, 0, _m.Gap, 0) : new Thickness(0, 0, 0, _m.Gap);

    private void Press(double to)
    {
        _press.BeginAnimation(ScaleTransform.ScaleXProperty, Spring(to));
        _press.BeginAnimation(ScaleTransform.ScaleYProperty, Spring(to));
    }

    private static DoubleAnimation Spring(double to)
        => new(to, TimeSpan.FromMilliseconds(300)) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 } };

    public void Render(CellView view)
    {
        Last = view;
        if (_face is null || _kind != view.Kind)
        {
            _faceSlot.Children.Clear();
            _kind = view.Kind;
            _face = view.Kind switch
            {
                CellKind.Value => new ValueCell(),
                CellKind.Status => new StatusCell(),
                CellKind.Sparkline => new SparklineCell(),
                _ => new RingCell(),   // Group : l'anneau de l'enfant en tête
            };
            _faceSlot.Children.Add(_face);
        }
        _face.Render(view, _m);
        _caption.Text = view.Caption ?? "";
        _caption.FontSize = (view.Kind == CellKind.Status ? 11 : 15) * _m.Scale;
        _caption.Opacity = view.Stale ? 0.55 : 1;
        // Hidden, pas Collapsed : la hauteur (m.Caption) reste réservée pour que le pas des cellules ne bouge pas
        // selon que chacune montre sa légende ou non.
        _caption.Visibility = view.ShowCaption ? Visibility.Visible : Visibility.Hidden;
        ToolTip = null;   // la carte hover remplace l'infobulle
    }
}
