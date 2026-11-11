using System.ComponentModel;

namespace Tower.Controls;

[ToolboxItem(true)]
[DefaultProperty(nameof(CursorFile))]
public partial class HotspotPanel : Panel
{
    private string _cursorFile = string.Empty;
    private Cursor? _customCursor;

    public HotspotPanel()
    {
        BackColor = Color.Transparent;
        BorderStyle = BorderStyle.None;
        Size = new Size(100, 40);
    }

    [Category("Hotspot")]
    [Description("Selecteer een .cur-bestand voor de cursor.")]
    [DefaultValue("")]
    [Editor(
        "System.Windows.Forms.Design.FileNameEditor, System.Design",
        "System.Drawing.Design.UITypeEditor, System.Drawing")]
    public string CursorFile
    {
        get => _cursorFile;
        set
        {
            value ??= string.Empty;

            // Alleen de bestandsnaam bewaren,
            // geen absoluut pad van de ontwikkelcomputer.
            _cursorFile = Path.GetFileName(value);

            LoadCustomCursor(value);
        }
    }

    private void LoadCustomCursor(string selectedPath)
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "Resources/Cursors",
            _cursorFile
        );

        if (!File.Exists(path))
        {
            System.Diagnostics.Debug.WriteLine($"Cursor niet gevonden: {path}");

            Cursor = Cursors.Default;
            return;
        }

        var newCursor = new Cursor(path);

        Cursor = newCursor;

        var oldCursor = _customCursor;
        _customCursor = newCursor;

        oldCursor?.Dispose();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        // Ook na initialisatie opnieuw proberen.
        if (_customCursor == null &&
            !string.IsNullOrWhiteSpace(_cursorFile))
        {
            LoadCustomCursor(string.Empty);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Cursor = Cursors.Default;
            _customCursor?.Dispose();
            _customCursor = null;
        }

        base.Dispose(disposing);
    }
}
