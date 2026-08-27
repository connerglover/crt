using CRT.Services;
using CRT.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace CRT.Views;

/// <summary>The collapsible loads/segments panel shared by both retimer pages.</summary>
public sealed partial class SessionSidebar : UserControl
{
    public SessionSidebar()
    {
        InitializeComponent();
    }

    public SessionViewModel VM => AppServices.Session;

    public Visibility IsEmptyVisible(bool hasRows) =>
        hasRows ? Visibility.Collapsed : Visibility.Visible;

    private void OnRowFieldLostFocus(object sender, RoutedEventArgs e) =>
        CommitRowField((TextBox)sender);

    private void OnRowFieldKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            CommitRowField((TextBox)sender);
        }
    }

    private static void CommitRowField(TextBox box)
    {
        if (box.DataContext is not RangeRowViewModel row)
        {
            return;
        }
        if ((string?)box.Tag == "start")
        {
            row.StartText = box.Text;
        }
        else
        {
            row.EndText = box.Text;
        }
        row.Commit();
    }
}
