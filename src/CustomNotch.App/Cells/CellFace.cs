using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using CustomNotch.App.Notch;
using CustomNotch.Core.Model;

namespace CustomNotch.App.Cells;

/// <summary>Le carré de la cellule (Ring × Ring) : chaque type dessine dedans. Les deux animations d'activité sont
/// communes : un arc fin qui tourne (Busy) et une pulsation ambre (Attention).</summary>
public abstract class CellFace : Grid
{
    private readonly Path _activity = new() { StrokeThickness = 2.5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Visibility = Visibility.Collapsed, RenderTransformOrigin = new Point(0.5, 0.5) };
    private readonly RotateTransform _spin = new();
    private bool _spinning;

    protected CellFace()
    {
        _activity.RenderTransform = _spin;
        Children.Add(_activity);
        Panel.SetZIndex(_activity, 10);
    }

    public abstract void Render(CellView view, PillMetrics m);

    /// <summary>Le glyph au centre, 26/44 de la cellule, blanc cassé (ou grisé si périmé).</summary>
    protected static Path Glyph(string? name, PillMetrics m, bool stale)
    {
        var (data, filled) = GlyphLibrary.Get(name);
        var size = m.Ring * 26 / 44;
        var brush = StatusPalette.Frozen(stale ? StatusPalette.Dim : StatusPalette.Ink);
        var path = filled ? Glyphs.Fill(data, size, brush) : Glyphs.Stroke(data, size, brush, 1.6);
        path.Width = size; path.Height = size;
        return path;
    }

    /// <summary>« dot » : Busy/Attention ne colorent que la pastille de la face (StatusCell) ou la teinte du statut
    /// (RingCell) — pas d'arc, pas d'animation qui tourne. « ring » : l'arc animé d'aujourd'hui, en rond ou, en
    /// forme carrée, le long du contour de SquareArc (qui tourne comme le rond : ça reste lisible autour du centre).</summary>
    protected void Activity(Status status, PillMetrics m, string activity, string shape)
    {
        Width = m.Ring; Height = m.Ring;
        if (activity != "ring" || status is not (Status.Busy or Status.Attention))
        {
            StopSpin();
            _activity.BeginAnimation(OpacityProperty, null);
            _activity.Visibility = Visibility.Collapsed;
            return;
        }
        var square = shape == "square";
        var r = m.Ring / 2 - 1.25;
        var side = m.Ring - 2.5;
        var squareRadius = SquareArc.Radius(m.Ring);
        _activity.Data = square
            ? SquareArc.Geometry(status == Status.Busy ? 0.25 : 1, side, squareRadius)
            : RingArc.Geometry(status == Status.Busy ? 0.25 : 1, r);
        _activity.Width = square ? side : 2 * r; _activity.Height = square ? side : 2 * r;
        _activity.Stroke = StatusPalette.Brush(status);
        _activity.Visibility = Visibility.Visible;
        if (status == Status.Busy && !_spinning)
        {
            _spinning = true;
            _spin.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.2)) { RepeatBehavior = RepeatBehavior.Forever });
            _activity.BeginAnimation(OpacityProperty, null);
            _activity.Opacity = 1;
        }
        else if (status == Status.Attention)
        {
            StopSpin();
            _activity.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.25, TimeSpan.FromSeconds(0.55)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
        }
    }

    private void StopSpin()
    {
        if (!_spinning) return;
        _spinning = false;
        _spin.BeginAnimation(RotateTransform.AngleProperty, null);
        _spin.Angle = 0;
    }
}
