using CommunityToolkit.Mvvm.Messaging.Messages;

namespace GuardianOS.Messages;

/// <summary>
/// Mensaje para solicitar la navegación de vuelta al Dashboard.
/// </summary>
public class NavigateToDashboardMessage : ValueChangedMessage<bool>
{
    public NavigateToDashboardMessage() : base(true) { }
}
