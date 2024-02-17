using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MlinetlesHyperNative;

public partial class Chat : UserControl
{
    public Chat()
    {
        InitializeComponent();
    }

    void OpenMenu(object sender, RoutedEventArgs e)
    {
        menu.IsVisible = !menu.IsVisible;
        var icon = (PathIcon)((Button)sender).Content!;
        if (menu.IsVisible) icon.Classes.Add("open");
        else icon.Classes.Remove("open");
    }

    void Exit(object sender, RoutedEventArgs e)
    {
        ((SplitView)Parent!).IsPaneOpen = false;
    }
}