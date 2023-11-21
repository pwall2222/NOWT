using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;

namespace NOWT.Views;

public partial class Info : UserControl
{
    public Info()
    {
        InitializeComponent();
    }

    private void HandleLinkClickAsync(object sender, RequestNavigateEventArgs e)
    {
        var link = (Hyperlink)sender;
        var navigateUri = link.NavigateUri.ToString();
        Process.Start(new ProcessStartInfo(navigateUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void ImageClickAsync(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        Process.Start(new ProcessStartInfo(button.Tag.ToString()) { UseShellExecute = true });
        e.Handled = true;
    }
}
