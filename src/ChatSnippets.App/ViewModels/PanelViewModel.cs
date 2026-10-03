using System.Collections.ObjectModel;
using System.Windows.Input;
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

    public PanelViewModel(AppConfig config, IconStore icons)
    {
        Items = new ObservableCollection<SnippetViewModel>(config.Snippets.Select(s => new SnippetViewModel(s, icons)));
        _pinned = config.Window.Pinned;
        ExecuteCommand = new RelayCommand(p => ExecuteRequested?.Invoke((SnippetViewModel)p!));
        EditCommand = new RelayCommand(p => EditRequested?.Invoke((SnippetViewModel)p!));
        DeleteCommand = new RelayCommand(p => DeleteRequested?.Invoke((SnippetViewModel)p!));
        AddCommand = new RelayCommand(_ => AddRequested?.Invoke());
    }

    public ObservableCollection<SnippetViewModel> Items { get; }
    public bool Pinned { get => _pinned; set { if (Set(ref _pinned, value)) PinnedChanged?.Invoke(value); } }

    public ICommand ExecuteCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand AddCommand { get; }

    public event Action<SnippetViewModel>? ExecuteRequested;
    public event Action<SnippetViewModel>? EditRequested;
    public event Action<SnippetViewModel>? DeleteRequested;
    public event Action? AddRequested;
    public event Action<SnippetViewModel, SnippetViewModel>? MoveRequested;
    public event Action<bool>? PinnedChanged;

    public void RequestMove(SnippetViewModel from, SnippetViewModel to) => MoveRequested?.Invoke(from, to);
}
