using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using Traveler.Core.Interfaces;
using Traveler.Core.Models;

namespace Traveler.Desktop.ViewModels;

/// <summary>
/// ViewModel for the Vendors view with affordability and wishlist matching.
/// </summary>
public class VendorsViewModel : ViewModelBase
{
    private readonly IVendorsService _vendorsService;
    private VendorSale? _selectedSale;
    private bool _isLoading;
    private bool _showAffordableOnly;
    private bool _showWishlistOnly;

    public string Title => "Vendors";

    public ObservableCollection<VendorSale> Sales { get; } = new();

    public VendorSale? SelectedSale
    {
        get => _selectedSale;
        set => this.RaiseAndSetIfChanged(ref _selectedSale, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public bool ShowAffordableOnly
    {
        get => _showAffordableOnly;
        set
        {
            this.RaiseAndSetIfChanged(ref _showAffordableOnly, value);
            ApplyFilters();
        }
    }

    public bool ShowWishlistOnly
    {
        get => _showWishlistOnly;
        set
        {
            this.RaiseAndSetIfChanged(ref _showWishlistOnly, value);
            ApplyFilters();
        }
    }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    // Design-time constructor
    public VendorsViewModel()
    {
        _vendorsService = null!;
        RefreshCommand = null!;
    }

    public VendorsViewModel(IVendorsService vendorsService)
    {
        _vendorsService = vendorsService;
        RefreshCommand = ReactiveCommand.CreateFromTask(LoadVendorsAsync);
        
        // Auto-load
        _ = LoadVendorsAsync();
    }

    private async Task LoadVendorsAsync()
    {
        IsLoading = true;
        Sales.Clear();

        try
        {
            var sales = await _vendorsService.GetVendorSalesAsync();
            foreach (var sale in sales)
            {
                Sales.Add(sale);
            }
            ApplyFilters();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilters()
    {
        // In a real implementation, we'd use ICollectionView for filtering
        // For now, this is a placeholder
    }
}
