using System.Windows;
using System.Windows.Controls;

namespace NotepadPro.Controls;

public partial class DialogTitleBar : UserControl
{
    public DialogTitleBar()
    {
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Window.GetWindow(this)?.Close();
    }
}
