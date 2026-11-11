using System.ComponentModel;
using TowerSpeech.Core;
using TowerSpeech.WindowsAudio;

namespace Tower.WinForms.Forms;

public partial class Form_SoundSetup : Form
{
    private SpeechPreferences? preferences;
    private string dataDirectory = "";

    public Form_SoundSetup()
    {
        InitializeComponent();
    }

    private void Form_SoundSetup_Load(object? sender, EventArgs e)
    {
        if (DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime)
        {
            return;
        }

        try
        {
            CheckBox_Talk.Checked = Program.Settings.SoundTalk;
            dataDirectory = Program.Voice.SpeechWorkspace;
            preferences = CompiledSpeechData.Load(dataDirectory).Preferences.Copy();

            ComboBox_Engine.SelectedIndex = (int)preferences.Engine;
            ComboBox_Voice.Items.AddRange(WindowsSpeechRenderer.InstalledVoices().Cast<object>().ToArray());
            ComboBox_Voice.SelectedIndex = Math.Max(0, ComboBox_Voice.Items.IndexOf(preferences.VoiceName));
            NumericUpDown_Rate.Value = preferences.Rate;
            CheckBox_RadioEffect.Checked = preferences.RadioEffect;
            NumericUpDown_Noise.Value = preferences.NoisePercent;
            UpdateControlAvailability();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            MessageBox.Show(this, "Could not load speech settings. " + exception.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    private void SpeechSelectionChanged(object? sender, EventArgs e)
    {
        if (DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime)
        {
            return;
        }
        UpdateControlAvailability();
    }

    private void UpdateControlAvailability()
    {
        var talk = CheckBox_Talk.Checked;
        var windows = talk && ComboBox_Engine.SelectedIndex == (int)SpeechEngineKind.WindowsTts;
        foreach (var control in new Control[] { Label_Engine, ComboBox_Engine, Label_Rate, NumericUpDown_Rate })
        {
            control.Enabled = talk;
        }
        foreach (var control in new Control[] { Label_Voice, ComboBox_Voice, Label_RadioEffect, CheckBox_RadioEffect, Label_Noise, NumericUpDown_Noise })
        {
            control.Enabled = windows;
        }
    }

    private void Button_Save_Click(object? sender, EventArgs e)
    {
        if (preferences is null)
        {
            return;
        }

        try
        {
            preferences.Engine = (SpeechEngineKind)ComboBox_Engine.SelectedIndex;
            preferences.VoiceName = ComboBox_Voice.SelectedIndex == 0 ? "" : (string)ComboBox_Voice.SelectedItem!;
            preferences.Rate = (int)NumericUpDown_Rate.Value;
            preferences.RadioEffect = CheckBox_RadioEffect.Checked;
            preferences.NoisePercent = (int)NumericUpDown_Noise.Value;

            CompiledSpeechData.SavePreferences(dataDirectory, preferences);
            Program.Application.SetSoundTalk(CheckBox_Talk.Checked);

            Program.Voice.ReloadSpeechWorkspace();

            DialogResult = DialogResult.OK;

            Close();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(this, "Could not save speech settings. Check data permissions and setting values.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Button_Cancel_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;

        Close();
    }
}
