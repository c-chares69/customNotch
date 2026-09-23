using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CustomNotch.Core;
using WinForms = System.Windows.Forms;

namespace CustomNotch.App;

/// <summary>L'icône de la zone de notification : afficher/masquer les pilules, rafraîchir, réglages, quitter.
/// L'icône est dessinée au lancement (un anneau blanc sur fond noir) : pas de fichier .ico à ce stade.</summary>
public sealed class TrayIcon : IDisposable
{
    public event Action? RefreshRequested;
    public event Action? SettingsRequested;
    public event Action? QuitRequested;
    public event Action? ToggleAllRequested;
    public event Action<string>? PillToggleRequested;

    private readonly WinForms.NotifyIcon _icon;
    private readonly WinForms.ContextMenuStrip _menu;
    private readonly WinForms.ToolStripMenuItem _pills;

    public TrayIcon()
    {
        // Le rendu WinForms par défaut est toujours clair, quel que soit le thème de Windows : le menu est peint avec
        // la palette de l'application (Theme.CurrentPalette, relue à chaque dessin — un changement de thème se voit
        // à l'ouverture suivante). ToolStripManager : les sous-menus créés plus tard héritent du même rendu.
        WinForms.ToolStripManager.Renderer = new TrayMenuRenderer();
        _menu = new WinForms.ContextMenuStrip
        {
            Font = new System.Drawing.Font("Segoe UI", 10f), ShowImageMargin = false, ShowCheckMargin = true,
            Padding = new WinForms.Padding(6, 8, 6, 8),
        };
        _pills = new WinForms.ToolStripMenuItem("Pilules");
        _menu.Items.AddRange(new WinForms.ToolStripItem[]
        {
            new WinForms.ToolStripMenuItem($"{App_.Name} {App_.Version}") { Enabled = false },
            new WinForms.ToolStripSeparator(),
            _pills,
            Item("Afficher / masquer tout", () => ToggleAllRequested?.Invoke()),
            Item("Rafraîchir maintenant", () => RefreshRequested?.Invoke()),
            new WinForms.ToolStripSeparator(),
            Item("Réglages…", () => SettingsRequested?.Invoke()),
            Item("Quitter", () => QuitRequested?.Invoke()),
        });
        _icon = new WinForms.NotifyIcon { ContextMenuStrip = _menu, Text = App_.Name, Icon = DrawIcon(), Visible = true };
        _icon.MouseClick += (_, e) => { if (e.Button == WinForms.MouseButtons.Left) ToggleAllRequested?.Invoke(); };
    }

    private static class App_ { public const string Name = CustomNotch.Core.App.Name; public static readonly string Version = CustomNotch.Core.App.Version; }

    private static WinForms.ToolStripMenuItem Item(string text, Action action)
    {
        var item = new WinForms.ToolStripMenuItem(text);
        item.Click += (_, _) => action();
        return item;
    }

    public void SetPills(IEnumerable<(string Id, bool Visible)> pills)
    {
        _pills.DropDownItems.Clear();
        foreach (var (id, visible) in pills)
        {
            var item = new WinForms.ToolStripMenuItem(id) { Checked = visible };
            item.Click += (_, _) => PillToggleRequested?.Invoke(id);
            _pills.DropDownItems.Add(item);
        }
        _pills.Enabled = _pills.DropDownItems.Count > 0;
    }

    public void Notify(string title, string body) => _icon.ShowBalloonTip(5000, title, body, WinForms.ToolTipIcon.None);

    /// <summary>Un carré noir arrondi et un anneau blanc : la pilule vue de loin.</summary>
    private static System.Drawing.Icon DrawIcon()
    {
        const int size = 64;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRoundedRectangle(Brushes.Black, null, new Rect(4, 4, 56, 56), 16, 16);
            dc.DrawEllipse(null, new Pen(Brushes.White, 7), new Point(32, 32), 15, 15);
        }
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        stream.Position = 0;
        using var png = new System.Drawing.Bitmap(stream);
        return System.Drawing.Icon.FromHandle(png.GetHicon());
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }
}
