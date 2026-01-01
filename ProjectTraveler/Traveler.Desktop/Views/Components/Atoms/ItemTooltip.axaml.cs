using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Traveler.Desktop.Views.Components.Atoms;

public partial class ItemTooltip : UserControl
{
    public ItemTooltip()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
