using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Traveler.Desktop.ViewModels;

namespace Traveler.Desktop;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        if (data is null) return null;
        
        var name = data.GetType().FullName!.Replace("ViewModel", "View");
        var type = Type.GetType(name);

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase || data is CommunityToolkit.Mvvm.ComponentModel.ObservableObject;
    }
}
