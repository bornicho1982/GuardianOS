using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using Traveler.Core.Models;

namespace Traveler.Desktop.Converters;

/// <summary>
/// Compares two CharacterInfo objects to determine if they are the same (for selected state).
/// Returns true if the character IDs match.
/// </summary>
public class IsSelectedCharacterConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return false;
        
        var currentCharacter = values[0] as CharacterInfo;
        var selectedCharacter = values[1] as CharacterInfo;
        
        if (currentCharacter == null || selectedCharacter == null)
            return false;
            
        return currentCharacter.CharacterId == selectedCharacter.CharacterId;
    }
}

/// <summary>
/// Returns gold border for selected character, dark border for others.
/// </summary>
public class SelectedCharacterBorderConverter : IMultiValueConverter
{
    private static readonly SolidColorBrush SelectedBrush = new(Color.Parse("#EBC805"));
    private static readonly SolidColorBrush UnselectedBrush = new(Color.Parse("#404040"));
    
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return UnselectedBrush;
        
        var currentCharacter = values[0] as CharacterInfo;
        var selectedCharacter = values[1] as CharacterInfo;
        
        if (currentCharacter == null || selectedCharacter == null)
            return UnselectedBrush;
            
        return currentCharacter.CharacterId == selectedCharacter.CharacterId 
            ? SelectedBrush 
            : UnselectedBrush;
    }
}
