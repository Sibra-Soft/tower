using System.ComponentModel;
using Tower.Core;
using Tower.Infrastructure;

namespace Tower.WinForms.Forms;

public partial class Form_Performance : Form
{
    private Session? state;
    private ControllerProfile? controller;

    public Form_Performance()
    {
        InitializeComponent();
    }

    public void Configure(Session session, ControllerProfile? profile)
    {
        state = session;
        controller = profile;
    }

    private void Form_ControllerStats_Load(object? sender, EventArgs e)
    {
        if (DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime)
        {
            return;
        }

        controller ??= Program.Profiles.FirstOrDefault(profile => profile.Id == Program.Settings.SelectedController) ?? new ControllerProfile();
        
        var review = state is not null;

        Text = $"{(review ? "Performance Review" : "Controller Statistics")} - {controller.Name}";

        GroupBox_Airport.Visible = review;
        GroupBox_Scenario.Visible = review;
        GroupBox_Profile.Visible = !review;

        if (state is { } session)
        {
            GroupBox_Airport.Text = session.Airport.Name;
            TextBox_Difficulty.Text = session.Airport.Difficulty.ToString();

            var performance = session.Performance;

            Label_TaxiOperationsValue.Text = performance.TaxiOperations.ToString();
            Label_TakeoffLandingValue.Text = performance.TakeoffLandingCommands.ToString();
            Label_HandoffsValue.Text = performance.Handoffs.ToString();
            Label_HoldsValue.Text = performance.Holds.ToString();
            Label_ReportValue.Text = performance.Reports.ToString();
            Label_RequestValue.Text = performance.Requests.ToString();
            Label_BreakOutValue.Text = performance.Breakouts.ToString();
            Label_PatternValue.Text = performance.Patterns.ToString();
            Label_StraightInValue.Text = performance.StraightIns.ToString();
            Label_CategoryArrivalValue.Text = performance.CategorySeparationArrival.ToString();
            Label_RunwaySeparationValue.Text = performance.RunwaySeparation.ToString();
            Label_CategoryDepartureValue.Text = performance.CategorySeparationDeparture.ToString();
            Label_CrashesValue.Text = performance.Crashes.ToString();
            TextBox_Rating.Text = performance.Rating(session).ToString();
            
            Label_Status.Text = session.Options.Practice ? "Practice session - controller rating and rewards are unaffected."
                : session.ResultsRecorded ? "Results recorded - this session will not be credited again."
                : performance.HasOperationalErrors ? "Results are recorded when you end the simulation.\nOperational errors exclude a reward. Runway overruns: " + performance.RunwayOverruns + "."
                : controller.Sessions == 0 || controller.AverageRating <= 0 ? "Results are recorded when you end the simulation.\nYour first positive average establishes the baseline for future rewards."
                : "Results are recorded when you end the simulation.\nA reward requires no operational errors and at least 10% average-rating improvement.";
        }
        else
        {
            Label_ProfileSummary.Text = $"Sessions: {controller.Sessions}\nAverage rating: {controller.AverageRating}\n"
                + $"Arrivals: {controller.Arrivals}    Departures: {controller.Departures}\nIncidents: {controller.Incidents}";
            ListBox_Rewards.Items.Clear();
            
            foreach (var reward in PerformanceRewards.Catalogue)
            {
                var count = controller.RewardCounts.GetValueOrDefault(reward.Kind);
                if (count > 0)
                {
                    ListBox_Rewards.Items.Add($"{reward.Title} ({count})");
                }
            }

            Label_Status.Text = "Average rating uses the last ten rated sessions per airport.";
        }
    }
}
