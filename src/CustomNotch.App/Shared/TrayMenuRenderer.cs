using System.Drawing;
using System.Drawing.Drawing2D;
using WinForms = System.Windows.Forms;

namespace CustomNotch.App;

/// <summary>Le menu de l'icône de la barre des tâches (WinForms) peint avec la palette de
/// l'application : fond, survol, séparateurs, coche en accent, sans la gouttière claire du
/// rendu Windows. Les couleurs sont lues à chaque dessin : un changement de thème se voit
/// à l'ouverture suivante, sans rien recréer.</summary>
internal sealed class TrayMenuRenderer : WinForms.ToolStripProfessionalRenderer
{
    public TrayMenuRenderer() : base(new WinForms.ProfessionalColorTable { UseSystemColors = false })
    {
        RoundedEdges = false;
    }

    private static Color D(System.Windows.Media.Color c) => Color.FromArgb(c.A, c.R, c.G, c.B);
    private static Theme.Palette P => Theme.CurrentPalette;

    protected override void OnRenderToolStripBackground(WinForms.ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(D(P.Surface));
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderImageMargin(WinForms.ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(D(P.Surface));
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(WinForms.ToolStripRenderEventArgs e)
    {
        var b = P.Border;
        // La bordure de la palette est translucide : posée sur le fond, elle donne un gris discret.
        var bg = D(P.Surface);
        var mixed = Color.FromArgb((bg.R * (255 - b.A) + b.R * b.A) / 255, (bg.G * (255 - b.A) + b.G * b.A) / 255, (bg.B * (255 - b.A) + b.B * b.A) / 255);
        using var pen = new Pen(mixed);
        var r = e.AffectedBounds;
        e.Graphics.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
    }

    protected override void OnRenderMenuItemBackground(WinForms.ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled) return;
        var r = new Rectangle(2, 0, e.Item.Width - 4, e.Item.Height - 1);
        using var path = Rounded(r, 4);
        using var brush = new SolidBrush(D(P.SurfaceHover));
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillPath(brush, path);
    }

    protected override void OnRenderItemText(WinForms.ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? D(P.Fg) : D(P.Subtle);
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(WinForms.ToolStripSeparatorRenderEventArgs e)
    {
        var b = P.Border;
        var bg = D(P.Surface);
        var mixed = Color.FromArgb((bg.R * (255 - b.A) + b.R * b.A) / 255, (bg.G * (255 - b.A) + b.G * b.A) / 255, (bg.B * (255 - b.A) + b.B * b.A) / 255);
        using var pen = new Pen(mixed);
        var y = e.Item.Height / 2;
        e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
    }

    protected override void OnRenderItemCheck(WinForms.ToolStripItemImageRenderEventArgs e)
    {
        using var pen = new Pen(D(Theme.CurrentAccent), 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        var r = e.ImageRectangle;
        var cx = r.X + r.Width / 2f;
        var cy = r.Y + r.Height / 2f;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawLines(pen, new[] { new PointF(cx - 5, cy), new PointF(cx - 1.5f, cy + 3.5f), new PointF(cx + 5, cy - 3.5f) });
    }

    protected override void OnRenderArrow(WinForms.ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = D(P.Muted);
        base.OnRenderArrow(e);
    }

    private static GraphicsPath Rounded(Rectangle r, int radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
