using System.ComponentModel;
using System.Globalization;
using Tower.Infrastructure;

namespace Tower.WinForms.Forms;

public partial class Form_BRITESetup : Form
{
    private DisplaySettings? displaySettings;
    private bool focusRangeMarks;
    private DisplaySettings Settings => displaySettings ?? Program.Settings;

    public Form_BRITESetup()
    {
        InitializeComponent();
    }

    internal void ConfigureRange(DisplaySettings settings, bool rangeMarks)
    {
        displaySettings = settings;
        focusRangeMarks = rangeMarks;
    }

    private static double Number(TextBox textBox, double minimum, double maximum)
    {
        if ((!double.TryParse(textBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
            !double.TryParse(textBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)) ||
            !double.IsFinite(value) || value < minimum || value > maximum)
        {
            throw new InvalidOperationException($"Enter a value between {minimum} and {maximum}.");
        }
        return value;
    }

    private void Form_BRITESetup_Load(object sender, EventArgs e)
    {
        if (DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime)
        {
            return;
        }
        TextBox_Range.Text = Settings.BriteRange.ToString("0.00", CultureInfo.InvariantCulture);
        TextBox_RangeMarks.Text = Settings.RangeMarks.ToString(CultureInfo.InvariantCulture);
        CheckBox_Obstructions.Checked = Settings.BriteObstructions;
        CheckBox_VideoMap.Checked = Settings.BriteMap;
        CheckBox_RangeMarks.Checked = Settings.ShowRangeMarks;
        CheckBox_Taxiways.Checked = Settings.BriteTaxiways;
    }

    private void Form_BRITESetup_Shown(object? sender, EventArgs e)
    {
        var field = focusRangeMarks ? TextBox_RangeMarks : TextBox_Range;
        field.Focus();
        field.SelectAll();
    }

    private void Button_OK_Click(object sender, EventArgs e)
    {
        try
        {
            var range = Number(TextBox_Range, 2, 50);
            var marks = Number(TextBox_RangeMarks, 0.1, 25);
            Settings.RangeMarks = marks;
            Settings.BriteRange = range;
            Settings.BriteObstructions = CheckBox_Obstructions.Checked;
            Settings.BriteMap = CheckBox_VideoMap.Checked;
            Settings.ShowRangeMarks = CheckBox_RangeMarks.Checked;
            Settings.BriteTaxiways = CheckBox_Taxiways.Checked;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (InvalidOperationException exception)
        {
            MessageBox.Show(this, exception.Message, "BRITE Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void Button_Cancel_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }
}
