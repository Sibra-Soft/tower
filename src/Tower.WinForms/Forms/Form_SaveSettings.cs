using System.ComponentModel;
using Tower.Infrastructure;

namespace Tower.WinForms.Forms;

public partial class Form_SaveSettings : Form
{
    private WindowLayoutStore? store;
    private WindowLayout? layout;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string SavedFileName { get; private set; } = "";

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool MakeDefault => CheckBox_MakeDefault.Checked;

    public Form_SaveSettings()
    {
        InitializeComponent();
    }

    public void Configure(WindowLayoutStore storage, WindowLayout snapshot, string selected)
    {
        store = storage;
        layout = snapshot;
        ListBox_Files.Items.Clear();
        ListBox_Files.Items.AddRange(storage.List());
        TextBox_FileName.Text = selected;
    }

    private void ListBox_Files_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (ListBox_Files.SelectedItem is string file)
        {
            TextBox_FileName.Text = file;
        }
    }

    private void Button_OK_Click(object? sender, EventArgs e)
    {
        if (store is null || layout is null)
        {
            return;
        }
        try
        {
            var path = store.FilePath(TextBox_FileName.Text);
            if (File.Exists(path) && MessageBox.Show(this, "Replace the existing layout?", Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }
            store.Write(TextBox_FileName.Text, layout);
            SavedFileName = Path.GetFileName(path);
            DialogResult = DialogResult.OK;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(this, "Could not save the layout. " + exception.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
