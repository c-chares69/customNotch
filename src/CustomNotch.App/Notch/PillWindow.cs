using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using CustomNotch.Core.Config;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace CustomNotch.App.Notch;

/// <summary>Une fenêtre par pilule : transparente, au-dessus de tout, jamais activée (WS_EX_NOACTIVATE | TOOLWINDOW),
/// hors barre des tâches et Alt-Tab. La fenêtre est plus grande que la pilule : elle réserve, côté libre, la place de
/// la carte hover et de sa queue, dessinées dans la même fenêtre pour ne jamais gérer de z-order. Les pixels
/// transparents laissent passer les clics.</summary>
public sealed class PillWindow : Window
{
    private readonly IPillHost _host;
    private readonly Canvas _canvas = new();
    private readonly Path _shape = new() { Fill = Brushes.Black, Stroke = new SolidColorBrush(Color.FromRgb(0x2e, 0x2e, 0x2e)), StrokeThickness = 1 };
    private readonly StackPanel _cells = new();
    private readonly Rectangle _grip = new() { Width = 4, Height = 28, RadiusX = 2, RadiusY = 2, Fill = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255)) };
    private PillMetrics _m;
    private Point? _dragFrom;

    public PillWindow(PillConfig pill, IPillHost host)
    {
        _host = host;
        Pill = pill;
        _m = new PillMetrics(pill.Scale);
        Title = $"customNotch - {pill.Id}";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        _canvas.Children.Add(_shape);
        _canvas.Children.Add(_cells);
        _canvas.Children.Add(_grip);
        Content = _canvas;
        SourceInitialized += (_, _) => NoActivate();
        Loaded += (_, _) => Reposition();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        Closed += (_, _) => Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        _shape.MouseEnter += (_, _) => _grip.Fill = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255));
        _shape.MouseLeave += (_, _) => { if (_dragFrom is null) _grip.Fill = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255)); };
        _grip.MouseEnter += (_, _) => _grip.Fill = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255));
        _grip.Cursor = Cursors.SizeAll;
        _grip.MouseLeftButtonDown += OnDragStart;
        _grip.MouseMove += OnDragMove;
        _grip.MouseLeftButtonUp += OnDragEnd;
        _shape.MouseRightButtonUp += (_, e) => { ShowMenu(); e.Handled = true; };
        Layout();
    }

    public PillConfig Pill { get; private set; }
    private int CellCount => Math.Max(1, Pill.Cells.Count(c => c.Visible));
    private bool Vertical => EdgePlacement.IsVertical(Pill.Edge);

    private void OnDisplayChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(Reposition);

    private void NoActivate()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var style = Native.GetWindowLong(hwnd, Native.GwlExStyle);
        Native.SetWindowLong(hwnd, Native.GwlExStyle, style | Native.WsExToolWindow | Native.WsExNoActivate);
    }

    public void Apply(PillConfig pill)
    {
        Pill = pill;
        _m = new PillMetrics(pill.Scale);
        Layout();
        if (IsLoaded) Reposition();
    }

    /// <summary>Taille de la fenêtre et position de chaque élément dans le canvas. Le canvas de la pilule est posé
    /// contre le bord ; la réserve de la carte est côté libre.</summary>
    private void Layout()
    {
        var extent = _m.Extent(CellCount);
        var reserve = _m.CardWidth + _m.Tail + _m.CardGap;
        _shape.Data = PillShape.Build(_m, Pill.Edge, CellCount);
        if (Vertical)
        {
            Width = _m.Width + reserve;
            Height = Math.Max(extent, 480);
            var top = (Height - extent) / 2;
            var left = Pill.Edge == "right" ? reserve : 0;
            Canvas.SetLeft(_shape, left); Canvas.SetTop(_shape, top);
            _cells.Orientation = Orientation.Vertical;
            Canvas.SetLeft(_cells, left); Canvas.SetTop(_cells, top + _m.Fillet + _m.Padding);
            _cells.Width = _m.Width;
            _grip.Width = 4; _grip.Height = 28;
            Canvas.SetLeft(_grip, left + (Pill.Edge == "right" ? 6 : _m.Width - 10)); Canvas.SetTop(_grip, top + _m.Fillet + 4);
        }
        else
        {
            Width = Math.Max(extent, 480);
            Height = _m.Width + reserve;
            var left = (Width - extent) / 2;
            var top = Pill.Edge == "bottom" ? reserve : 0;
            Canvas.SetLeft(_shape, left); Canvas.SetTop(_shape, top);
            _cells.Orientation = Orientation.Horizontal;
            Canvas.SetLeft(_cells, left + _m.Fillet + _m.Padding); Canvas.SetTop(_cells, top);
            _cells.Height = _m.Width;
            _grip.Width = 28; _grip.Height = 4;
            Canvas.SetLeft(_grip, left + _m.Fillet + 4); Canvas.SetTop(_grip, top + (Pill.Edge == "bottom" ? 6 : _m.Width - 10));
        }
    }

    /// <summary>L'écran demandé (ou le principal), sa zone de travail en DIP, et la position qui en découle.</summary>
    public void Reposition()
    {
        var screen = Screen();
        var area = Screens.ToDip(this, screen.WorkingArea);
        var extent = _m.Extent(CellCount);
        var pill = EdgePlacement.Place(area, Pill.Edge, Pill.Along, extent, _m.Width);
        MoveTo(pill);
    }

    private void MoveTo(Rect pill)
    {
        var reserve = _m.CardWidth + _m.Tail + _m.CardGap;
        var extent = _m.Extent(CellCount);
        (Left, Top) = Pill.Edge switch
        {
            "right" => (pill.X - reserve, pill.Y - (Height - extent) / 2),
            "left" => (pill.X, pill.Y - (Height - extent) / 2),
            "top" => (pill.X - (Width - extent) / 2, pill.Y),
            _ => (pill.X - (Width - extent) / 2, pill.Y - reserve),
        };
    }

    private System.Windows.Forms.Screen Screen()
    {
        var all = System.Windows.Forms.Screen.AllScreens;
        return all.FirstOrDefault(s => s.DeviceName == Pill.Screen) ?? System.Windows.Forms.Screen.PrimaryScreen ?? all[0];
    }

    // ---- drag le long du bord (et d'un écran à l'autre) ----------------------------------------------------------

    private void OnDragStart(object sender, MouseButtonEventArgs e)
    {
        _dragFrom = e.GetPosition(this);
        _grip.CaptureMouse();
        e.Handled = true;
    }

    private void OnDragMove(object sender, MouseEventArgs e)
    {
        if (_dragFrom is not { } from) return;
        var cursor = Screens.CursorDip(this);
        var (sx, sy) = Screens.Scale(this);
        var device = new System.Drawing.Point((int)Math.Round(cursor.X * sx), (int)Math.Round(cursor.Y * sy));
        var dragArea = Screens.WorkArea(this, device);
        var extent = _m.Extent(CellCount);
        // La pilule suit le curseur le long du bord de l'écran sous la souris ; l'autre axe reste collé au bord.
        var origin = Vertical ? new Point(0, cursor.Y - from.Y + (Height - extent) / 2) : new Point(cursor.X - from.X + (Width - extent) / 2, 0);
        var along = EdgePlacement.AlongFrom(dragArea, Pill.Edge, origin, extent);
        MoveTo(EdgePlacement.Place(dragArea, Pill.Edge, along, extent, _m.Width));
    }

    /// <summary>Recalcule depuis le curseur actuel, pas depuis un état laissé par OnDragMove : sans ça, un
    /// clic-relâché sans déplacement (jamais de MouseMove) ou un survol rapide vers un autre écran juste avant
    /// le relâché se retrouvait avec une zone ou un écran périmés (voire la zone par défaut, along=0).</summary>
    private void OnDragEnd(object sender, MouseButtonEventArgs e)
    {
        if (_dragFrom is null) return;
        _dragFrom = null;
        _grip.ReleaseMouseCapture();
        var cursor = Screens.CursorDip(this);
        var (sx, sy) = Screens.Scale(this);
        var device = new System.Drawing.Point((int)Math.Round(cursor.X * sx), (int)Math.Round(cursor.Y * sy));
        var area = Screens.WorkArea(this, device);
        var extent = _m.Extent(CellCount);
        var origin = Vertical ? new Point(0, Top + (Height - extent) / 2) : new Point(Left + (Width - extent) / 2, 0);
        var along = EdgePlacement.AlongFrom(area, Pill.Edge, origin, extent);
        var screen = System.Windows.Forms.Screen.FromPoint(device).DeviceName;
        _host.SavePosition(Pill.Id, along, screen);
    }

    private void ShowMenu()
    {
        var menu = new ContextMenu();
        void Add(string text, Action action) { var item = new MenuItem { Header = text }; item.Click += (_, _) => action(); menu.Items.Add(item); }
        Add("Rafraîchir cette pilule", () => { foreach (var c in Pill.Cells) _host.RequestRefresh(c.Id); });
        Add("Masquer cette pilule", () => _host.HidePill(Pill.Id));
        menu.Items.Add(new Separator());
        Add("Réglages…", _host.ShowSettings);
        menu.IsOpen = true;
    }

    public void UpdateCell(string cellId) { }
    public void Tick() { }
}
