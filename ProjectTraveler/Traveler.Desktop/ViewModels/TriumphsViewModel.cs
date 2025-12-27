using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using Traveler.Core.Interfaces;
using Traveler.Core.Models;

namespace Traveler.Desktop.ViewModels;

/// <summary>
/// ViewModel for the Triumphs/Records view with recursive tree structure.
/// </summary>
public class TriumphsViewModel : ViewModelBase
{
    private readonly ITriumphsService _triumphsService;
    private Triumph? _selectedTriumph;
    private bool _isLoading;
    private bool _showCompleted = true;

    public string Title => "Triumphs";

    public ObservableCollection<Triumph> RootNodes { get; } = new();

    public Triumph? SelectedTriumph
    {
        get => _selectedTriumph;
        set => this.RaiseAndSetIfChanged(ref _selectedTriumph, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public bool ShowCompleted
    {
        get => _showCompleted;
        set => this.RaiseAndSetIfChanged(ref _showCompleted, value);
    }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    // Design-time constructor
    public TriumphsViewModel()
    {
        _triumphsService = null!;
        RefreshCommand = null!;
    }

    public TriumphsViewModel(ITriumphsService triumphsService)
    {
        _triumphsService = triumphsService;
        RefreshCommand = ReactiveCommand.CreateFromTask(LoadTriumphsAsync);
        
        // Auto-load on construction
        _ = LoadTriumphsAsync();
    }

    private async Task LoadTriumphsAsync()
    {
        IsLoading = true;
        RootNodes.Clear();

        try
        {
            var tree = await _triumphsService.GetTriumphTreeAsync();
            foreach (var node in tree)
            {
                RootNodes.Add(node);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}
