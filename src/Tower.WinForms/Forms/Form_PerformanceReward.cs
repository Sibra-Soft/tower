using Tower.Core;

namespace Tower.WinForms.Forms;

public partial class Form_PerformanceReward : Form
{
    public Form_PerformanceReward()
    {
        InitializeComponent();
    }

    public void Configure(PerformanceReward reward)
    {
        Text = reward.Title;
        Label_Title.Text = reward.Title;
        Label_Message.Text = reward.Message;
    }
}
