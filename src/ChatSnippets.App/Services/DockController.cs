using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using ChatSnippets.App.Interop;
using ChatSnippets.App.Views;
using ChatSnippets.Core;

namespace ChatSnippets.App.Services;

/// <summary>Выезд/уезд панели, флажок, перетаскивание за шапку, таймер ухода мыши. Всё в физических пикселях.</summary>
internal sealed class DockController
{
    const int PanelDips = 80, FlagWidthDips = 16, FlagHeightDips = 64;
    static readonly TimeSpan SlideTime = TimeSpan.FromMilliseconds(180);
    static readonly TimeSpan LeaveDelay = TimeSpan.FromSeconds(1.5);

    readonly PanelWindow _panel;
    readonly FlagWindow _flag;
    readonly WindowSettings _settings;
    readonly Func<int> _itemCount;
    readonly Action _save;
    readonly DispatcherTimer _hoverTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    readonly DispatcherTimer _slideTimer = new() { Interval = TimeSpan.FromMilliseconds(15) };

    IntPtr _panelHwnd, _flagHwnd;
    bool _expanded, _dragging;
    DateTime _outsideSince = DateTime.UtcNow;
    double _slideProgress;
    int _slideFromX, _slideToX;
    Action? _slideDone;
    Native.POINT _dragStartCursor;
    int _dragStartLeft, _dragStartTop;

    public DockController(PanelWindow panel, FlagWindow flag, WindowSettings settings, Func<int> itemCount, Action save)
    {
        _panel = panel; _flag = flag; _settings = settings; _itemCount = itemCount; _save = save;
        _hoverTimer.Tick += (_, _) => OnHoverTick();
        _slideTimer.Tick += (_, _) => OnSlideTick();
        _flag.Clicked += Toggle;
        _panel.MinimizeClicked += Collapse;
        _panel.SideChosen += SetSide;
        var title = _panel.TitleBarElement;
        title.MouseLeftButtonDown += TitleBar_Down;
        title.MouseMove += TitleBar_Move;
        title.MouseLeftButtonUp += TitleBar_Up;
        title.LostMouseCapture += TitleBar_LostCapture;
        // Монитор отключили, сменили разрешение/масштаб или сдвинули панель задач — заново прижимаемся к краю.
        SystemEvents.DisplaySettingsChanged += (_, _) => _panel.Dispatcher.BeginInvoke(Relayout);
        SystemEvents.UserPreferenceChanged += (_, _) => _panel.Dispatcher.BeginInvoke(Relayout);
    }

    public bool IsExpanded => _expanded;
    public bool Suspended { get; set; }
    bool Pinned => _settings.Pinned;

    public void Start()
    {
        _panel.Show();
        _flag.Show();
        _panelHwnd = new WindowInteropHelper(_panel).Handle;
        _flagHwnd = new WindowInteropHelper(_flag).Handle;
        _expanded = false;
        Relayout();
        _hoverTimer.Start();
    }

    public Task FlashFlagAsync() => _flag.FlashAsync();

    public void Toggle() { if (_expanded) Collapse(); else Expand(); }

    public void Expand()
    {
        if (_expanded) return;
        _expanded = true;
        _outsideSince = DateTime.UtcNow;
        var m = Measure();
        Native.ShowWindow(_panelHwnd, Native.SW_SHOWNOACTIVATE);
        _flag.SetDirection(_settings.Side == DockSide.Right);       // стрелка смотрит наружу: «спрятать»
        StartSlide(DockLogic.PanelLeft(m.Work, _settings.Side, false, m.PanelW),
                   DockLogic.PanelLeft(m.Work, _settings.Side, true, m.PanelW), done: null);
    }

    public void Collapse()
    {
        if (!_expanded) return;
        _expanded = false;
        var m = Measure();
        _flag.SetDirection(_settings.Side == DockSide.Left);        // стрелка смотрит внутрь экрана: «выдвинуть»
        StartSlide(DockLogic.PanelLeft(m.Work, _settings.Side, true, m.PanelW),
                   DockLogic.PanelLeft(m.Work, _settings.Side, false, m.PanelW),
                   done: () => Native.ShowWindow(_panelHwnd, Native.SW_HIDE)); // не светиться на соседнем мониторе
    }

    public void SetSide(DockSide side)
    {
        _settings.Side = side;
        _save();
        Relayout();
    }

    /// <summary>Пересчитать размеры и позиции без анимации (старт, смена числа иконок/стороны/DPI).</summary>
    public void Relayout()
    {
        _slideTimer.Stop();
        var m = Measure();
        _settings.Top = DockLogic.ClampTop(m.Work, m.Work.Top + _settings.Top, m.PanelH) - m.Work.Top;
        var panelX = DockLogic.PanelLeft(m.Work, _settings.Side, _expanded, m.PanelW);
        Native.SetWindowPos(_panelHwnd, Native.HWND_TOPMOST, panelX, m.Work.Top + _settings.Top, m.PanelW, m.PanelH, Native.SWP_NOACTIVATE);
        PlaceFlag(m, _expanded);
        _flag.SetDirection(_expanded ? _settings.Side == DockSide.Right : _settings.Side == DockSide.Left);
        Native.ShowWindow(_panelHwnd, _expanded ? Native.SW_SHOWNOACTIVATE : Native.SW_HIDE);
    }

    // ---- анимация ----
    void StartSlide(int fromX, int toX, Action? done)
    {
        _slideFromX = fromX; _slideToX = toX; _slideDone = done; _slideProgress = 0;
        _slideTimer.Start();
    }

    void OnSlideTick()
    {
        _slideProgress = Math.Min(1, _slideProgress + _slideTimer.Interval / SlideTime);
        var eased = 1 - Math.Pow(1 - _slideProgress, 3);           // ease-out cubic
        var m = Measure();
        var x = (int)Math.Round(_slideFromX + (_slideToX - _slideFromX) * eased);
        Native.SetWindowPos(_panelHwnd, Native.HWND_TOPMOST, x, m.Work.Top + _settings.Top, m.PanelW, m.PanelH, Native.SWP_NOACTIVATE);
        PlaceFlagFollowing(m, x);
        if (_slideProgress >= 1)
        {
            _slideTimer.Stop();
            _slideDone?.Invoke();
            PlaceFlag(m, _expanded);
        }
    }

    // ---- флажок ----
    void PlaceFlag(Metrics m, bool expanded) =>
        Native.SetWindowPos(_flagHwnd, Native.HWND_TOPMOST,
            DockLogic.FlagLeft(m.Work, _settings.Side, expanded, m.PanelW, m.FlagW),
            m.Work.Top + _settings.Top, m.FlagW, m.FlagH, Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);

    /// <summary>Во время анимации флажок едет вместе с панелью, но не выходит за край экрана.</summary>
    void PlaceFlagFollowing(Metrics m, int panelX)
    {
        var x = _settings.Side == DockSide.Left ? panelX + m.PanelW : panelX - m.FlagW;
        x = _settings.Side == DockSide.Left ? Math.Max(x, m.Work.Left) : Math.Min(x, m.Work.Right - m.FlagW);
        Native.SetWindowPos(_flagHwnd, Native.HWND_TOPMOST, x, m.Work.Top + _settings.Top, m.FlagW, m.FlagH,
            Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
    }

    // ---- таймер ухода мыши ----
    void OnHoverTick()
    {
        if (!_expanded || Pinned || Suspended || _dragging) { _outsideSince = DateTime.UtcNow; return; }
        Native.GetCursorPos(out var p);
        var m = Measure();
        var panelRect = new PxRect(DockLogic.PanelLeft(m.Work, _settings.Side, true, m.PanelW), m.Work.Top + _settings.Top, m.PanelW, m.PanelH);
        var flagRect = new PxRect(DockLogic.FlagLeft(m.Work, _settings.Side, true, m.PanelW, m.FlagW), m.Work.Top + _settings.Top, m.FlagW, m.FlagH);
        if (panelRect.Contains(p.X, p.Y) || flagRect.Contains(p.X, p.Y)) { _outsideSince = DateTime.UtcNow; return; }
        if (DateTime.UtcNow - _outsideSince >= LeaveDelay) Collapse();
    }

    // ---- перетаскивание за шапку ----
    void TitleBar_Down(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not (System.Windows.Controls.Border or System.Windows.Controls.StackPanel)) return; // клики по кнопкам не тащат
        _dragging = true;
        Native.GetCursorPos(out _dragStartCursor);
        var m = Measure();
        _dragStartLeft = DockLogic.PanelLeft(m.Work, _settings.Side, _expanded, m.PanelW);
        _dragStartTop = m.Work.Top + _settings.Top;
        _flag.Hide();
        ((UIElement)sender).CaptureMouse();
    }

    void TitleBar_Move(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        Native.GetCursorPos(out var p);
        var m = Measure();
        Native.SetWindowPos(_panelHwnd, Native.HWND_TOPMOST,
            _dragStartLeft + p.X - _dragStartCursor.X, _dragStartTop + p.Y - _dragStartCursor.Y,
            m.PanelW, m.PanelH, Native.SWP_NOACTIVATE);
    }

    /// <summary>Захват мыши отняли (UAC, блокировка экрана): перетаскивание не должно «зависнуть».</summary>
    void TitleBar_LostCapture(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        Relayout();
    }

    void TitleBar_Up(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        ((UIElement)sender).ReleaseMouseCapture();
        Native.GetCursorPos(out var p);
        var m = Measure(Native.MonitorFromPoint(p, Native.MONITOR_DEFAULTTONEAREST));   // монитор, куда отпустили панель
        var dx = p.X - _dragStartCursor.X;
        var dy = p.Y - _dragStartCursor.Y;
        if (Math.Abs(dx) > 3 || Math.Abs(dy) > 3)      // простой клик по шапке — не перетаскивание
        {
            _settings.Side = DockLogic.NearestSide(m.Work, _dragStartLeft + dx, m.PanelW);
            _settings.Top = DockLogic.ClampTop(m.Work, _dragStartTop + dy, m.PanelH) - m.Work.Top;
            _save();
        }
        Relayout();
    }

    // ---- измерения ----
    readonly record struct Metrics(PxRect Work, double Scale, int PanelW, int PanelH, int FlagW, int FlagH);

    /// <summary>
    /// Монитор берём по флажку (он всегда на экране), а не по панели: спрятанная панель стоит за краем
    /// и «ближайшим» мог оказаться соседний монитор.
    /// </summary>
    Metrics Measure(IntPtr? monitorOverride = null)
    {
        var monitor = monitorOverride ?? Native.MonitorFromWindow(_flagHwnd, Native.MONITOR_DEFAULTTONEAREST);
        var info = new Native.MONITORINFO { cbSize = Marshal.SizeOf<Native.MONITORINFO>() };
        Native.GetMonitorInfo(monitor, ref info);
        var work = new PxRect(info.rcWork.Left, info.rcWork.Top, info.rcWork.Right - info.rcWork.Left, info.rcWork.Bottom - info.rcWork.Top);
        var scale = Native.GetDpiForMonitor(monitor, 0, out var dpiX, out _) == 0 ? dpiX / 96.0 : VisualTreeHelper.GetDpi(_panel).DpiScaleX;
        return new Metrics(work, scale,
            (int)Math.Round(PanelDips * scale),
            DockLogic.PanelHeightPx(work, _itemCount() + 1, scale),
            (int)Math.Round(FlagWidthDips * scale),
            (int)Math.Round(FlagHeightDips * scale));
    }
}
