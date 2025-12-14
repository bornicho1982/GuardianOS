using CommunityToolkit.Mvvm.Messaging.Messages;
using GuardianOS.Models;

namespace GuardianOS.Messages;

/// <summary>
/// Mensaje enviado cuando se selecciona un personaje en el Dashboard para ver sus detalles.
/// </summary>
public class CharacterSelectedMessage : ValueChangedMessage<DestinyCharacter>
{
    public CharacterSelectedMessage(DestinyCharacter character) : base(character)
    {
    }
}
