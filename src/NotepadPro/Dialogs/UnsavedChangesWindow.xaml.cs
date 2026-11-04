using System.Windows;

namespace NotepadPro.Dialogs;

public enum UnsavedChoice
{
    Save,
    Discard,
    Cancel
}

public partial class UnsavedChangesWindow : DialogWindow
{
    public UnsavedChangesWindow()
    {
        InitializeComponent();
    }

    public UnsavedChoice Choice { get; private set; } = UnsavedChoice.Cancel;

    public static UnsavedChoice Ask(Window owner, string documentName, string? filePath)
    {
        var dialog = new UnsavedChangesWindow
        {
            Owner = owner
        };
        dialog.HeadlineText.Text = $"Do you want to save changes to {documentName}?";
        dialog.DetailText.Text = string.IsNullOrEmpty(filePath)
            ? "This document has never been saved."
            : filePath;
        dialog.ShowDialog();
        return dialog.Choice;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Choice = UnsavedChoice.Save;
        DialogResult = true;
    }

    private void Discard_Click(object sender, RoutedEventArgs e)
    {
        Choice = UnsavedChoice.Discard;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Choice = UnsavedChoice.Cancel;
        DialogResult = false;
    }
}
