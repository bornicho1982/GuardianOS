using ReactiveUI;
using Traveler.Core.Interfaces;

namespace Traveler.Desktop.ViewModels;

public class BuildArchitectViewModel : ViewModelBase
{
    private readonly IBuildCopilotService _buildCopilotService;
    public string Title => "AI Build Architect";

    // Design-time constructor
    public BuildArchitectViewModel() { _buildCopilotService = null!; }

    public BuildArchitectViewModel(IBuildCopilotService buildCopilotService)
    {
        _buildCopilotService = buildCopilotService;
    }
}
