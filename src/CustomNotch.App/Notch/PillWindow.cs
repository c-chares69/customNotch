using System.Windows;
using CustomNotch.Core.Config;

namespace CustomNotch.App.Notch;

public sealed class PillWindow : Window
{
    public PillWindow(PillConfig pill, IPillHost host)
    {
        Pill = pill;
        Title = $"customNotch - {pill.Id}";
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = System.Windows.Media.Brushes.Transparent;
        ShowInTaskbar = false; Topmost = true; ShowActivated = false; ResizeMode = ResizeMode.NoResize;
        Width = 100; Height = 300; Left = 200; Top = 200;
        Content = new System.Windows.Shapes.Rectangle { Fill = System.Windows.Media.Brushes.Black, RadiusX = 20, RadiusY = 20, Width = 70, Height = 260 };
    }

    public PillConfig Pill { get; private set; }
    public void Apply(PillConfig pill) => Pill = pill;
    public void UpdateCell(string cellId) { }
    public void Tick() { }
}
