using Avalonia.Controls;

namespace MlinetlesHyperNative.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    void NavMenuSwitch(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        navMenu.IsPaneOpen = !navMenu.IsPaneOpen;
        ((PathIcon)button.Content!).Data = GetIconFromName(navMenu.IsPaneOpen ? "chevron_left_regular" : "chevron_right_regular")!;
    }

    void NavMenuClick(object sender, RoutedEventArgs e)
    {
        var button = (RadioButton)sender;
        switch ((string)button.Content!)
        {
            case "主页":
                tabControl.SelectedIndex = 0; break;
            case "加密":
                tabControl.SelectedIndex = 1; break;
            case "解密":
                tabControl.SelectedIndex = 2; break;
        }
    }

    void ChatClick(object sender, RoutedEventArgs e)
    {
        chat.IsPaneOpen = true;
    }

    public void SetContent(Control control)
    {
        control.Margin = new Thickness(20);
        mainLayOut.Children[^1] = control;
    }
}
