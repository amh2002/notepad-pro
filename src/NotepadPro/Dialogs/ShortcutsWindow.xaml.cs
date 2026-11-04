using System.Windows;

namespace NotepadPro.Dialogs;

public sealed record ShortcutEntry(string Action, string Keys);

public sealed record ShortcutGroup(string Name, IReadOnlyList<ShortcutEntry> Items);

public partial class ShortcutsWindow : DialogWindow
{
    public ShortcutsWindow()
    {
        InitializeComponent();
        Groups.ItemsSource = new List<ShortcutGroup>
        {
            new("File", [
                new("New tab", "Ctrl+N"),
                new("Open file", "Ctrl+O"),
                new("Save", "Ctrl+S"),
                new("Save as", "Ctrl+Shift+S"),
                new("Save all", "Ctrl+Alt+S"),
                new("Close tab", "Ctrl+W"),
                new("Close window", "Ctrl+Shift+W"),
                new("Reopen closed tab", "Ctrl+Shift+T")
            ]),
            new("Tabs and navigation", [
                new("Next tab", "Ctrl+Tab"),
                new("Previous tab", "Ctrl+Shift+Tab"),
                new("Go to line", "Ctrl+G"),
                new("Move to start of document", "Ctrl+Home"),
                new("Move to end of document", "Ctrl+End")
            ]),
            new("Editing", [
                new("Undo", "Ctrl+Z"),
                new("Redo", "Ctrl+Y"),
                new("Duplicate line", "Ctrl+D"),
                new("Delete line", "Ctrl+Shift+K"),
                new("Move line up", "Alt+Up"),
                new("Move line down", "Alt+Down"),
                new("Toggle comment", "Ctrl+/"),
                new("Indent selection", "Tab"),
                new("Outdent selection", "Shift+Tab"),
                new("Insert date and time", "F5")
            ]),
            new("Search", [
                new("Find", "Ctrl+F"),
                new("Replace", "Ctrl+H"),
                new("Find next", "F3"),
                new("Find previous", "Shift+F3"),
                new("Close search panel", "Esc")
            ]),
            new("View", [
                new("Zoom in", "Ctrl+Plus"),
                new("Zoom out", "Ctrl+Minus"),
                new("Reset zoom", "Ctrl+0"),
                new("Toggle word wrap", "Ctrl+`"),
                new("Toggle whitespace", "Ctrl+Shift+8"),
                new("Toggle status bar", "Ctrl+Shift+B")
            ])
        };
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
