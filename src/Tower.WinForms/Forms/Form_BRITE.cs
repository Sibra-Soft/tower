using Tower.Controls;
using Tower.Core;
using Tower.Infrastructure;

namespace Tower.WinForms.Forms;

public partial class Form_BRITE : Form
{
    private RadarControl? radar;
    private DisplaySettings? displaySettings;

    public Form_BRITE()
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
        Menu_Display_VideoMap.Checked = settings.BriteMap;
        Menu_Display_Taxiways.Checked = settings.BriteTaxiways;
        Menu_Display_Obstructions.Checked = settings.BriteObstructions;
        Menu_Display_RangeMarks.Checked = settings.ShowRangeMarks;
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
            case 3525:
                settings.BriteMap = !settings.BriteMap;
                break;
            case 3526:
                settings.BriteTaxiways = !settings.BriteTaxiways;
                break;
            case 3527:
                settings.BriteObstructions = !settings.BriteObstructions;
                break;
            case 3528:
                settings.ShowRangeMarks = !settings.ShowRangeMarks;
                break;
            case 3529:
                radar.ResetCenter();
                break;
            case 3530:
                radar.SetCenterOnNextClick(!radar.CenterOnNextClick);
                break;
            case 3531:
            case 3625:
                radar.ZoomIn();
                break;
            case 3532:
            case 3626:
                radar.ZoomOut();
                break;
            case 3533:
                ShowRangeSettings(false);
                break;
            case 3534:
                ShowRangeSettings(true);
                break;
        }
        radar.Invalidate();
        RefreshMenuChecks();
    }

    private void ShowRangeSettings(bool rangeMarks)
    {
        using var dialog = new Form_BRITESetup();
        dialog.Text = "BRITE" + (rangeMarks ? " Range Marks" : " Range");
        dialog.ConfigureRange(displaySettings!, rangeMarks);
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
