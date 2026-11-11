using Tower.Controls;
using Tower.Core;
using Tower.Infrastructure;

namespace Tower.WinForms.Forms;

public partial class Form_ASDE : Form
{
    private RadarControl? radar;
    private DisplaySettings? displaySettings;

    public Form_ASDE()
    {
        InitializeComponent();
        MenuStrip.Renderer = new MenuRenderer();
    }

    public void BindRadar(RadarControl control, DisplaySettings settings)
    {
        if (radar is not null)
        {
            radar.ViewChanged -= RefreshMenuChecks;
        }
        radar = control;
        displaySettings = settings;
        Panel_Radar.Controls.Add(control);
        radar.ViewChanged += RefreshMenuChecks;
        RefreshMenuChecks();
    }

    private void Menu_Display_DropDownOpening(object? sender, EventArgs e) => RefreshMenuChecks();

    private void RefreshMenuChecks()
    {
        if (displaySettings is not { } settings || radar is null)
        {
            return;
        }
        Menu_Display_VideoMap.Checked = settings.AsdeMap;
        Menu_Display_Taxiways.Checked = settings.AsdeTaxiways;
        Menu_Display_RunwayNames.Checked = settings.RunwayNames;
        Menu_Display_TaxiwayNames.Checked = settings.TaxiNames;
        Menu_Display_GateNames.Checked = settings.AsdeGateNames;
        Menu_Display_MouseCenter.Checked = radar.CenterOnNextClick;
    }

    private void Menu_Action_Click(object? sender, EventArgs e)
    {
        if (radar is null || displaySettings is not { } settings || sender is not ToolStripMenuItem { Tag: int id })
        {
            return;
        }
        switch (id)
        {
            case 3334:
                settings.AsdeGateNames = !settings.AsdeGateNames;
                break;
            case 3325:
                settings.AsdeMap = !settings.AsdeMap;
                break;
            case 3326:
                settings.AsdeTaxiways = !settings.AsdeTaxiways;
                break;
            case 3327:
                settings.RunwayNames = !settings.RunwayNames;
                break;
            case 3328:
                settings.TaxiNames = !settings.TaxiNames;
                break;
            case 3329:
                radar.ResetCenter();
                break;
            case 3330:
                radar.SetCenterOnNextClick(!radar.CenterOnNextClick);
                break;
            case 3331:
            case 3425:
                radar.ZoomIn();
                break;
            case 3332:
            case 3426:
                radar.ZoomOut();
                break;
            case 3333:
                ShowRangeSettings(false);
                break;
        }
        radar.Invalidate();
        RefreshMenuChecks();
    }

    private void ShowRangeSettings(bool rangeMarks)
    {
        using var dialog = new Form_ASDESetup();
        dialog.Text = "ASDE" + (rangeMarks ? " Range Marks" : " Range");
        dialog.ConfigureRange(displaySettings!);
        dialog.ShowDialog(this);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (radar?.HandleZoomKey(keyData) == true)
        {
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
