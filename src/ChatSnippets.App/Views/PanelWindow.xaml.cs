using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ChatSnippets.App.Interop;
using ChatSnippets.App.ViewModels;
using ChatSnippets.Core;

namespace ChatSnippets.App.Views;

public partial class PanelWindow : Window
{
    Point _dragOrigin;

    public event Action? MinimizeClicked;
    public event Action<DockSide>? SideChosen;
    public event Action? ExitRequested;

    public PanelWindow(PanelViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        SourceInitialized += (_, _) => NoActivate.Apply(this);
    }

    PanelViewModel Vm => (PanelViewModel)DataContext;

    public Border TitleBarElement => TitleBar;

    /// <summary>Подсказки открываются в сторону экрана, а не за его край.</summary>
    public void ApplySide(DockSide side)
    {
        ToolTipService.SetPlacement(this, side == DockSide.Right ? PlacementMode.Left : PlacementMode.Right);
        ToolTipService.SetHorizontalOffset(this, side == DockSide.Right ? -14 : 14);
    }

    void MinimizeButton_Click(object sender, RoutedEventArgs e) => MinimizeClicked?.Invoke();

    void GearButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        var layout = new MenuItem { Header = "Layout fix hotkey..." };
        layout.Click += (_, _) => Vm.LayoutHotkeyCommand.Execute(null);
        var left = new MenuItem { Header = "Left side" };
        left.Click += (_, _) => SideChosen?.Invoke(DockSide.Left);
        var right = new MenuItem { Header = "Right side" };
        right.Click += (_, _) => SideChosen?.Invoke(DockSide.Right);
        var exit = new MenuItem { Header = "Exit" };
        exit.Click += (_, _) => ExitRequested?.Invoke();
        menu.Items.Add(layout);
        menu.Items.Add(new Separator());
        menu.Items.Add(left);
        menu.Items.Add(right);
        menu.Items.Add(new Separator());
        menu.Items.Add(exit);
        menu.PlacementTarget = (UIElement)sender;
        menu.IsOpen = true;
    }

    // DataContext пункта контекстного меню — плитка, на которую кликнули правой кнопкой.
    void EditMenu_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is SnippetViewModel vm) Vm.EditCommand.Execute(vm);
    }

    void DeleteMenu_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is SnippetViewModel vm) Vm.DeleteCommand.Execute(vm);
    }

    void LayoutHotkeyMenu_Click(object sender, RoutedEventArgs e) => Vm.LayoutHotkeyCommand.Execute(null);

    void Snippet_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => _dragOrigin = e.GetPosition(null);

    void Snippet_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not Button { DataContext: SnippetViewModel vm }) return;
        var d = e.GetPosition(null) - _dragOrigin;
        if (Math.Abs(d.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(d.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        DragDrop.DoDragDrop((DependencyObject)sender, vm, DragDropEffects.Move);
    }

    void Snippet_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(SnippetViewModel)) is SnippetViewModel from
            && sender is Button { DataContext: SnippetViewModel to } && from != to)
            Vm.RequestMove(from, to);
    }
}
