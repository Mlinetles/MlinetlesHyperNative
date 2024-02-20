using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MlinetlesHyperNative;

public partial class Encrypt : UserControl
{
    public Encrypt()
    {
        InitializeComponent();
    }

    void Copy(object sender, RoutedEventArgs e)
    {
        encryptInput.SelectAll();
        encryptInput.Copy();
    }

    void Paste(object sender, RoutedEventArgs e)
    {
        encryptInput.Text = null;
        encryptInput.Paste();
    }
}