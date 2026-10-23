using Avalonia.Controls;
using Avalonia.Interactivity;
namespace RvzConverter.App;
public partial class AboutWindow : Window
{
    public AboutWindow() => InitializeComponent();
    private void CloseClick(object? sender, RoutedEventArgs e) => Close();
}
