using System.Runtime.InteropServices;

using System.ComponentModel;
using Tower.Infrastructure;

namespace Tower.Controls;

public partial class CommandTextBox : TextBox
{
    private readonly CommandInputBuffer buffer = new();
    private readonly Stack<string> undo = new();
    private bool updating;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string RawText => buffer.RawText;

    [DefaultValue(false)]
    public bool ExpandCommands
    {
        get => buffer.Expanded;
        set
        {
            if (buffer.Expanded == value)
            {
                return;
            }
            buffer.SetExpanded(value);
            RenderInput(buffer.DisplayText.Length);
        }
    }

    protected override void OnTextChanged(EventArgs e)
    {
        if (!updating)
        {
            buffer.SetText(base.Text);
            undo.Clear();
            if (ExpandCommands && base.Text != buffer.DisplayText)
            {
                RenderInput(buffer.DisplayText.Length);
                return;
            }
        }
        base.OnTextChanged(e);
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        if (ExpandCommands && !ReadOnly && !char.IsControl(e.KeyChar))
        {
            Insert(e.KeyChar.ToString());
            e.Handled = true;
        }
        base.OnKeyPress(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (ExpandCommands && !ReadOnly && (e.Modifiers is Keys.None or Keys.Control) && (e.KeyCode is Keys.Back or Keys.Delete))
        {
            undo.Push(buffer.RawText);
            var caret = buffer.Erase(SelectionStart, SelectionLength, e.KeyCode == Keys.Back);
            RenderInput(caret);
            e.SuppressKeyPress = true;
        }
        base.OnKeyDown(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (ExpandCommands && !ReadOnly)
        {
            switch (m.Msg)
            {
                case 0x302: // WM_PASTE
                    if (Clipboard.ContainsText())
                    {
                        Insert(Clipboard.GetText());
                    }
                    return;
                case 0x300: // WM_CUT
                    if (SelectionLength > 0)
                    {
                        Clipboard.SetText(SelectedText);
                        Insert("");
                    }
                    return;
                case 0x303: // WM_CLEAR
                    Insert("");
                    return;
                case 0x304: // WM_UNDO
                case 0xC7: // EM_UNDO
                    if (undo.TryPop(out var previous))
                    {
                        buffer.SetText(previous);
                        RenderInput(buffer.DisplayText.Length);
                    }
                    return;
            }
        }
        base.WndProc(ref m);
    }

    private void Insert(string text)
    {
        undo.Push(buffer.RawText);
        var caret = buffer.ReplaceSelection(SelectionStart, SelectionLength, text);
        RenderInput(caret);
    }

    private void RenderInput(int caret)
    {
        updating = true;
        try
        {
            base.Text = buffer.DisplayText;
            Select(Math.Clamp(caret, 0, TextLength), 0);
        }
        finally
        {
            updating = false;
        }
    }

    [DllImport("user32.dll")]
    private static extern bool CreateCaret(
        IntPtr hWnd,
        IntPtr hBitmap,
        int width,
        int height);

    [DllImport("user32.dll")]
    private static extern bool ShowCaret(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetCaretPos(out Point point);

    [DllImport("user32.dll")]
    private static extern bool SetCaretPos(int x, int y);

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);

        // Bewaar de huidige caretpositie.
        GetCaretPos(out Point position);

        // Bepaal de breedte van één karakter.
        int width = TextRenderer.MeasureText(
            "M",
            Font,
            Size.Empty,
            TextFormatFlags.NoPadding
        ).Width;

        // Vervang de standaardcaret.
        if (CreateCaret(
            Handle,
            IntPtr.Zero,
            width,
            Font.Height))
        {
            SetCaretPos(position.X, position.Y);
            ShowCaret(Handle);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}
