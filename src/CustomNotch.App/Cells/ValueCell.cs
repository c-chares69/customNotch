using System.Windows.Shapes;
using CustomNotch.App.Notch;
using CustomNotch.Core.Model;

namespace CustomNotch.App.Cells;

/// <summary>Un disque à piste fine et le glyph : la valeur, elle, est dans la légende sous la cellule.</summary>
public sealed class ValueCell : CellFace
{
    private readonly Ellipse _disc = new() { Fill = StatusPalette.Frozen(StatusPalette.Track) };
    private Path? _glyph;

    public ValueCell() => Children.Add(_disc);

    public override void Render(CellView view, PillMetrics m)
    {
        _disc.Width = _disc.Height = m.Ring;
        _disc.Opacity = view.Stale ? 0.55 : 1;
        if (_glyph is not null) Children.Remove(_glyph);
        _glyph = Glyph(view.Glyph, m, view.Stale);
        Children.Add(_glyph);
        Activity(view.Status, m);
    }
}
