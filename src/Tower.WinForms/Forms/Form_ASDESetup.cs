using System.ComponentModel;
using System.Globalization;
using Tower.Infrastructure;

namespace Tower.WinForms.Forms;

public partial class Form_ASDESetup : Form
{
    private DisplaySettings? displaySettings;
    private DisplaySettings Settings => displaySettings ?? Program.Settings;

    public Form_ASDESetup()
    {
        InitializeComponent();
    }

    internal void ConfigureRange(DisplaySettings settings)
    {
        displaySettings = settings;
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

    private void Form_ASDESetup_Load(object sender, EventArgs e)
    {
        if (DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime)
        {
            return;
        }
        TextBox_Range.Text = Settings.AsdeRange.ToString("0.00", CultureInfo.InvariantCulture);
        CheckBox_Taxiways.Checked = Settings.AsdeTaxiways;
        CheckBox_VideoMap.Checked = Settings.AsdeMap;
        CheckBox_TaxiNames.Checked = Settings.TaxiNames;
        CheckBox_RunwayNames.Checked = Settings.RunwayNames;
        CheckBox_GateNames.Checked = Settings.AsdeGateNames;
    }

    private void Form_ASDESetup_Shown(object? sender, EventArgs e)
    {
        var field = TextBox_Range;
        field.Focus();
        field.SelectAll();
    }

    private void Button_OK_Click(object sender, EventArgs e)
    {
        try
        {
            var range = Number(TextBox_Range, 0.1, 10);
            Settings.AsdeRange = range;
            Settings.AsdeMap = CheckBox_VideoMap.Checked;
            Settings.AsdeTaxiways = CheckBox_Taxiways.Checked;
            Settings.TaxiNames = CheckBox_TaxiNames.Checked;
            Settings.RunwayNames = CheckBox_RunwayNames.Checked;
            Settings.AsdeGateNames = CheckBox_GateNames.Checked;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (InvalidOperationException exception)
        {
            MessageBox.Show(this, exception.Message, "ASDE Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void Button_Cancel_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }
}
