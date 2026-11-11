using System.Diagnostics;

namespace Tower.WinForms.Forms
{
    public partial class Form_About : Form
    {
        public Form_About()
        {
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (DesignMode || System.ComponentModel.LicenseManager.UsageMode == System.ComponentModel.LicenseUsageMode.Designtime)
            {
                return;
            }
            Label1.Text = "Tower Version " + Form_Update.CurrentVersion.Split('+')[0];
        }

        private void Label2_Click(object sender, EventArgs e)
        {

        }

        private void LinkLabel_Website_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.tower-sim.com",
                UseShellExecute = true
            });
        }
    }
}
