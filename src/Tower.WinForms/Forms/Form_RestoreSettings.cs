using System.ComponentModel;
using Tower.Infrastructure;

namespace Tower.WinForms.Forms;

public partial class Form_RestoreSettings : Form
{
    private WindowLayoutStore? store;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public WindowLayout? SelectedLayout { get; private set; }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string SelectedFileName { get; private set; } = "";

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool RestoreDefault { get; private set; }

    public Form_RestoreSettings()
    {
        InitializeComponent();
    }

    public void Configure(WindowLayoutStore storage, string selected)
    {
        store = storage;
        ListBox_Files.Items.Clear();
        ListBox_Files.Items.AddRange(storage.List());
        ListBox_Files.SelectedIndex = ListBox_Files.FindStringExact(selected);
        Button_OK.Enabled = ListBox_Files.SelectedIndex >= 0;
    }

    private void ListBox_Files_SelectedIndexChanged(object? sender, EventArgs e)
    {
        Button_OK.Enabled = ListBox_Files.SelectedIndex >= 0;
    }

    private void Button_OK_Click(object? sender, EventArgs e)
    {
        if (store is null || ListBox_Files.SelectedItem is not string name)
        {
            return;
        }
        try
        {
            SelectedLayout = store.Read(name);
            SelectedFileName = name;
            DialogResult = DialogResult.OK;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(this, "Could not restore the layout. " + exception.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Button_Default_Click(object? sender, EventArgs e)
    {
        RestoreDefault = true;
        DialogResult = DialogResult.OK;
    }
}
