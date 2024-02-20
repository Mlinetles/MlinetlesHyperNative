using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MlinetlesHyperNative;

public partial class Decrypt : UserControl
{
    public Decrypt()
    {
        InitializeComponent();
    }

    void Paste(object sender, RoutedEventArgs e)
    {
        decryptInput.Text = null;
        decryptInput.Paste();
    }
}