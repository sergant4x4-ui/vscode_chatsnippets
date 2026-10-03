using System.Collections.ObjectModel;
using System.Windows.Input;
using ChatSnippets.App.Localization;
using ChatSnippets.Core;

namespace ChatSnippets.App.ViewModels;

public sealed class RelayCommand(Action<object?> run) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => run(parameter);
}

public sealed class PanelViewModel : ObservableBase
{
    bool _pinned;
    bool _editMode;

    public PanelViewModel(AppConfig config, IconStore icons)
    {
        Items = new ObservableCollection<SnippetViewModel>(config.Snippets.Select(s => new SnippetViewModel(s, icons)));
        _pinned = config.Window.Pinned;
        Items.CollectionChanged += (_, e) =>
        {
            if (e.NewItems is null) return;
            foreach (SnippetViewModel item in e.NewItems) item.EditMode = _editMode;   // новая плитка сразу в текущем режиме
        };
        ExecuteCommand = new RelayCommand(p => ExecuteRequested?.Invoke((SnippetViewModel)p!));
        EditCommand = new RelayCommand(p => EditRequested?.Invoke((SnippetViewModel)p!));
        DeleteCommand = new RelayCommand(p => DeleteRequested?.Invoke((SnippetViewModel)p!));
        AddCommand = new RelayCommand(_ => AddRequested?.Invoke());
        LayoutFixCommand = new RelayCommand(_ => LayoutFixRequested?.Invoke());
        LayoutHotkeyCommand = new RelayCommand(_ => LayoutHotkeyRequested?.Invoke());
        Loc.Changed += () =>
        {
            LayoutFix.Relocalize();
            foreach (var item in Items) item.Relocalize();
        };
    }

    public ObservableCollection<SnippetViewModel> Items { get; }
    /// <summary>Режим редактирования: клик по плитке открывает правку вместо вставки.</summary>
    public bool EditMode
    {
        get => _editMode;
        set
        {
            if (!Set(ref _editMode, value)) return;
            LayoutFix.EditMode = value;
            foreach (var item in Items) item.EditMode = value;
        }
    }
    public bool Pinned { get => _pinned; set { if (Set(ref _pinned, value)) PinnedChanged?.Invoke(value); } }

    public ICommand ExecuteCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand AddCommand { get; }
    public ICommand LayoutFixCommand { get; }
    public ICommand LayoutHotkeyCommand { get; }
    public LayoutFixViewModel LayoutFix { get; } = new();

    public event Action<SnippetViewModel>? ExecuteRequested;
    public event Action<SnippetViewModel>? EditRequested;
    public event Action<SnippetViewModel>? DeleteRequested;
    public event Action? AddRequested;
    public event Action? LayoutFixRequested;
    public event Action? LayoutHotkeyRequested;
    public event Action<SnippetViewModel, SnippetViewModel>? MoveRequested;
    public event Action<bool>? PinnedChanged;

    public void RequestMove(SnippetViewModel from, SnippetViewModel to) => MoveRequested?.Invoke(from, to);
}
