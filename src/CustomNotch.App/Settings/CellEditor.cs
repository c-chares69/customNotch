using System.Windows.Controls;
using CustomNotch.Core.Config;

namespace CustomNotch.App.Settings;

public sealed class CellEditor : UserControl
{
    public CellEditor(SettingsContext ctx, CellConfig cell, Action<Action> run) => Content = Ui.Text($"Cellule « {cell.Id} » — éditeur à la tâche 8", 14);
}
