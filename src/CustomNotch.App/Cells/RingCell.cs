using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using CustomNotch.App.Notch;
using CustomNotch.Core.Model;

namespace CustomNotch.App.Cells;

/// <summary>L'anneau de codenotch : piste grise, arc coloré par le statut, glyph au centre. En forme carrée, la
/// piste et l'arc suivent le contour d'un carré arrondi (SquareArc) au lieu du cercle ; les deux jeux d'éléments
/// existent toujours ensemble, `Render` bascule juste leur `Visibility` pour ne jamais recréer la face.</summary>
public sealed class RingCell : CellFace
{
    private readonly Ellipse _track = new() { Stroke = StatusPalette.Frozen(StatusPalette.Track), StrokeThickness = 5 };
    private readonly Path _arc = new() { StrokeThickness = 5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private readonly Path _squareTrack = new() { Stroke = StatusPalette.Frozen(StatusPalette.Track), StrokeThickness = 5, StrokeLineJoin = PenLineJoin.Round, Visibility = Visibility.Collapsed };
    private readonly Path _squareArc = new() { StrokeThickness = 5, StrokeLineJoin = PenLineJoin.Round, Visibility = Visibility.Collapsed };
    private Path? _glyph;

    public RingCell()
    {
        Children.Add(_track);
        Children.Add(_arc);
        Children.Add(_squareTrack);
        Children.Add(_squareArc);
    }

    public override void Render(CellView view, PillMetrics m)
    {
        var square = view.Shape == "square";
        var stroke = 5 * m.Scale;
        var brush = StatusPalette.Brush(view.Status is Status.Busy or Status.Attention ? Status.Ok : view.Status);
        var opacity = view.Stale ? 0.55 : 1;

        _track.Visibility = square ? Visibility.Collapsed : Visibility.Visible;
        _arc.Visibility = square ? Visibility.Collapsed : Visibility.Visible;
        _squareTrack.Visibility = square ? Visibility.Visible : Visibility.Collapsed;
        _squareArc.Visibility = square ? Visibility.Visible : Visibility.Collapsed;

        if (square)
        {
            // side/radius : mêmes proportions que le cercle (r = m.Ring/2 − stroke/2) pour que le trait déborde
            // symétriquement de la même quantité et que Path se centre pareil dans la cellule (Width = la portée
            // native de la géométrie, sans le débord du trait — comme _arc ci-dessous).
            var side = m.Ring - stroke;
            var radius = 12.0 / 44 * m.Ring - stroke / 2;
            _squareTrack.StrokeThickness = stroke;
            _squareTrack.Width = _squareTrack.Height = side;
            _squareTrack.Data = SquareArc.Geometry(1, side, radius);
            _squareArc.StrokeThickness = stroke;
            _squareArc.Width = _squareArc.Height = side;
            _squareArc.Data = SquareArc.Geometry(view.Fraction ?? 0, side, radius);
            _squareArc.Stroke = brush;
            _squareArc.Opacity = opacity;
            _squareTrack.Opacity = opacity;
        }
        else
        {
            var r = m.Ring / 2 - stroke / 2;
            _track.Width = _track.Height = 2 * r + stroke; _track.StrokeThickness = stroke;
            _arc.StrokeThickness = stroke;
            _arc.Width = _arc.Height = 2 * r;
            _arc.Data = RingArc.Geometry(view.Fraction ?? 0, r);
            _arc.Stroke = brush;
            _arc.Opacity = opacity;
            _track.Opacity = opacity;
        }

        if (_glyph is not null) Children.Remove(_glyph);
        _glyph = Glyph(view.Glyph, m, view.Stale);
        Children.Add(_glyph);
        Activity(view.Status, m, view.Activity, view.Shape);
    }
}
