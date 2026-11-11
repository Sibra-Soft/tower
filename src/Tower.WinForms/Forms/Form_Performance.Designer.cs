namespace Tower.WinForms.Forms;

public partial class Form_Performance
{
    private GroupBox GroupBox_Airport = null!;
    private GroupBox GroupBox_Scenario = null!;
    private GroupBox GroupBox_Profile = null!;
    private Label Label_Difficulty = null!;
    private TextBox TextBox_Difficulty = null!;
    private Label Label_Commands = null!;
    private Label Label_Errors = null!;
    private Label Label_Rating = null!;
    private TextBox TextBox_Rating = null!;
    private Label Label_Status = null!;
    private Label Label_ProfileSummary = null!;
    private ListBox ListBox_Rewards = null!;
    private Button Button_OK = null!;
    private Button Button_Cancel = null!;
    private Label Label_TaxiOperations = null!;
    private Label Label_TaxiOperationsValue = null!;
    private Label Label_TakeoffLanding = null!;
    private Label Label_TakeoffLandingValue = null!;
    private Label Label_Handoffs = null!;
    private Label Label_HandoffsValue = null!;
    private Label Label_Holds = null!;
    private Label Label_HoldsValue = null!;
    private Label Label_Report = null!;
    private Label Label_ReportValue = null!;
    private Label Label_Request = null!;
    private Label Label_RequestValue = null!;
    private Label Label_BreakOut = null!;
    private Label Label_BreakOutValue = null!;
    private Label Label_Pattern = null!;
    private Label Label_PatternValue = null!;
    private Label Label_StraightIn = null!;
    private Label Label_StraightInValue = null!;
    private Label Label_CategoryArrival = null!;
    private Label Label_CategoryArrivalValue = null!;
    private Label Label_RunwaySeparation = null!;
    private Label Label_RunwaySeparationValue = null!;
    private Label Label_CategoryDeparture = null!;
    private Label Label_CategoryDepartureValue = null!;
    private Label Label_Crashes = null!;
    private Label Label_CrashesValue = null!;

    private void InitializeComponent()
    {
        GroupBox_Airport = new GroupBox();
        Label_Difficulty = new Label();
        TextBox_Difficulty = new TextBox();
        GroupBox_Scenario = new GroupBox();
        Label_Commands = new Label();
        Label_Errors = new Label();
        Label_TaxiOperations = new Label();
        Label_TaxiOperationsValue = new Label();
        Label_TakeoffLanding = new Label();
        Label_TakeoffLandingValue = new Label();
        Label_Handoffs = new Label();
        Label_HandoffsValue = new Label();
        Label_Holds = new Label();
        Label_HoldsValue = new Label();
        Label_Report = new Label();
        Label_ReportValue = new Label();
        Label_Request = new Label();
        Label_RequestValue = new Label();
        Label_BreakOut = new Label();
        Label_BreakOutValue = new Label();
        Label_Pattern = new Label();
        Label_PatternValue = new Label();
        Label_StraightIn = new Label();
        Label_StraightInValue = new Label();
        Label_CategoryArrival = new Label();
        Label_CategoryArrivalValue = new Label();
        Label_RunwaySeparation = new Label();
        Label_RunwaySeparationValue = new Label();
        Label_CategoryDeparture = new Label();
        Label_CategoryDepartureValue = new Label();
        Label_Crashes = new Label();
        Label_CrashesValue = new Label();
        Label_Rating = new Label();
        TextBox_Rating = new TextBox();
        GroupBox_Profile = new GroupBox();
        Label_ProfileSummary = new Label();
        ListBox_Rewards = new ListBox();
        Label_Status = new Label();
        Button_OK = new Button();
        Button_Cancel = new Button();
        GroupBox_Airport.SuspendLayout();
        GroupBox_Scenario.SuspendLayout();
        GroupBox_Profile.SuspendLayout();
        SuspendLayout();
        // 
        // GroupBox_Airport
        // 
        GroupBox_Airport.Controls.Add(Label_Difficulty);
        GroupBox_Airport.Controls.Add(TextBox_Difficulty);
        GroupBox_Airport.Location = new Point(16, 16);
        GroupBox_Airport.Name = "GroupBox_Airport";
        GroupBox_Airport.Size = new Size(280, 74);
        GroupBox_Airport.TabIndex = 0;
        GroupBox_Airport.TabStop = false;
        GroupBox_Airport.Text = " Airport ";
        // 
        // Label_Difficulty
        // 
        Label_Difficulty.AutoSize = true;
        Label_Difficulty.Location = new Point(16, 32);
        Label_Difficulty.Name = "Label_Difficulty";
        Label_Difficulty.Size = new Size(66, 15);
        Label_Difficulty.TabIndex = 0;
        Label_Difficulty.Text = "Difficulty:";
        // 
        // TextBox_Difficulty
        // 
        TextBox_Difficulty.Location = new Point(172, 28);
        TextBox_Difficulty.Name = "TextBox_Difficulty";
        TextBox_Difficulty.ReadOnly = true;
        TextBox_Difficulty.Size = new Size(80, 21);
        TextBox_Difficulty.TabIndex = 1;
        TextBox_Difficulty.Text = "50";
        // 
        // GroupBox_Scenario
        // 
        GroupBox_Scenario.Controls.Add(Label_Commands);
        GroupBox_Scenario.Controls.Add(Label_Errors);
        GroupBox_Scenario.Controls.Add(Label_TaxiOperations);
        GroupBox_Scenario.Controls.Add(Label_TaxiOperationsValue);
        GroupBox_Scenario.Controls.Add(Label_TakeoffLanding);
        GroupBox_Scenario.Controls.Add(Label_TakeoffLandingValue);
        GroupBox_Scenario.Controls.Add(Label_Handoffs);
        GroupBox_Scenario.Controls.Add(Label_HandoffsValue);
        GroupBox_Scenario.Controls.Add(Label_Holds);
        GroupBox_Scenario.Controls.Add(Label_HoldsValue);
        GroupBox_Scenario.Controls.Add(Label_Report);
        GroupBox_Scenario.Controls.Add(Label_ReportValue);
        GroupBox_Scenario.Controls.Add(Label_Request);
        GroupBox_Scenario.Controls.Add(Label_RequestValue);
        GroupBox_Scenario.Controls.Add(Label_BreakOut);
        GroupBox_Scenario.Controls.Add(Label_BreakOutValue);
        GroupBox_Scenario.Controls.Add(Label_Pattern);
        GroupBox_Scenario.Controls.Add(Label_PatternValue);
        GroupBox_Scenario.Controls.Add(Label_StraightIn);
        GroupBox_Scenario.Controls.Add(Label_StraightInValue);
        GroupBox_Scenario.Controls.Add(Label_CategoryArrival);
        GroupBox_Scenario.Controls.Add(Label_CategoryArrivalValue);
        GroupBox_Scenario.Controls.Add(Label_RunwaySeparation);
        GroupBox_Scenario.Controls.Add(Label_RunwaySeparationValue);
        GroupBox_Scenario.Controls.Add(Label_CategoryDeparture);
        GroupBox_Scenario.Controls.Add(Label_CategoryDepartureValue);
        GroupBox_Scenario.Controls.Add(Label_Crashes);
        GroupBox_Scenario.Controls.Add(Label_CrashesValue);
        GroupBox_Scenario.Controls.Add(Label_Rating);
        GroupBox_Scenario.Controls.Add(TextBox_Rating);
        GroupBox_Scenario.Location = new Point(16, 106);
        GroupBox_Scenario.Name = "GroupBox_Scenario";
        GroupBox_Scenario.Size = new Size(580, 280);
        GroupBox_Scenario.TabIndex = 1;
        GroupBox_Scenario.TabStop = false;
        GroupBox_Scenario.Text = " Scenario ";
        // 
        // Label_Commands
        // 
        Label_Commands.AutoSize = true;
        Label_Commands.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold | FontStyle.Underline);
        Label_Commands.Location = new Point(16, 27);
        Label_Commands.Name = "Label_Commands";
        Label_Commands.Size = new Size(79, 15);
        Label_Commands.TabIndex = 0;
        Label_Commands.Text = "Commands";
        // 
        // Label_Errors
        // 
        Label_Errors.AutoSize = true;
        Label_Errors.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold | FontStyle.Underline);
        Label_Errors.Location = new Point(300, 27);
        Label_Errors.Name = "Label_Errors";
        Label_Errors.Size = new Size(46, 15);
        Label_Errors.TabIndex = 1;
        Label_Errors.Text = "Errors";
        // 
        // Label_TaxiOperations
        // 
        Label_TaxiOperations.AutoSize = true;
        Label_TaxiOperations.Location = new Point(16, 57);
        Label_TaxiOperations.Name = "Label_TaxiOperations";
        Label_TaxiOperations.Size = new Size(108, 15);
        Label_TaxiOperations.TabIndex = 2;
        Label_TaxiOperations.Text = "Taxi Operations";
        // 
        // Label_TaxiOperationsValue
        // 
        Label_TaxiOperationsValue.AutoSize = true;
        Label_TaxiOperationsValue.Location = new Point(230, 57);
        Label_TaxiOperationsValue.Name = "Label_TaxiOperationsValue";
        Label_TaxiOperationsValue.Size = new Size(15, 15);
        Label_TaxiOperationsValue.TabIndex = 3;
        Label_TaxiOperationsValue.Text = "0";
        Label_TaxiOperationsValue.TextAlign = ContentAlignment.TopRight;
        // 
        // Label_TakeoffLanding
        // 
        Label_TakeoffLanding.AutoSize = true;
        Label_TakeoffLanding.Location = new Point(16, 79);
        Label_TakeoffLanding.Name = "Label_TakeoffLanding";
        Label_TakeoffLanding.Size = new Size(90, 15);
        Label_TakeoffLanding.TabIndex = 4;
        Label_TakeoffLanding.Text = "Takeoff/Land";
        // 
        // Label_TakeoffLandingValue
        // 
        Label_TakeoffLandingValue.AutoSize = true;
        Label_TakeoffLandingValue.Location = new Point(230, 79);
        Label_TakeoffLandingValue.Name = "Label_TakeoffLandingValue";
        Label_TakeoffLandingValue.Size = new Size(15, 15);
        Label_TakeoffLandingValue.TabIndex = 5;
        Label_TakeoffLandingValue.Text = "0";
        Label_TakeoffLandingValue.TextAlign = ContentAlignment.TopRight;
        // 
        // Label_Handoffs
        // 
        Label_Handoffs.AutoSize = true;
        Label_Handoffs.Location = new Point(16, 101);
        Label_Handoffs.Name = "Label_Handoffs";
        Label_Handoffs.Size = new Size(64, 15);
        Label_Handoffs.TabIndex = 6;
        Label_Handoffs.Text = "Handoffs";
        // 
        // Label_HandoffsValue
        // 
        Label_HandoffsValue.AutoSize = true;
        Label_HandoffsValue.Location = new Point(230, 101);
        Label_HandoffsValue.Name = "Label_HandoffsValue";
        Label_HandoffsValue.Size = new Size(15, 15);
        Label_HandoffsValue.TabIndex = 7;
        Label_HandoffsValue.Text = "0";
        Label_HandoffsValue.TextAlign = ContentAlignment.TopRight;
        // 
        // Label_Holds
        // 
        Label_Holds.AutoSize = true;
        Label_Holds.Location = new Point(16, 123);
        Label_Holds.Name = "Label_Holds";
        Label_Holds.Size = new Size(44, 15);
        Label_Holds.TabIndex = 8;
        Label_Holds.Text = "Holds";
        // 
        // Label_HoldsValue
        // 
        Label_HoldsValue.AutoSize = true;
        Label_HoldsValue.Location = new Point(230, 123);
        Label_HoldsValue.Name = "Label_HoldsValue";
        Label_HoldsValue.Size = new Size(15, 15);
        Label_HoldsValue.TabIndex = 9;
        Label_HoldsValue.Text = "0";
        Label_HoldsValue.TextAlign = ContentAlignment.TopRight;
        // 
        // Label_Report
        // 
        Label_Report.AutoSize = true;
        Label_Report.Location = new Point(16, 145);
        Label_Report.Name = "Label_Report";
        Label_Report.Size = new Size(50, 15);
        Label_Report.TabIndex = 10;
        Label_Report.Text = "Report";
        // 
        // Label_ReportValue
        // 
        Label_ReportValue.AutoSize = true;
        Label_ReportValue.Location = new Point(230, 145);
        Label_ReportValue.Name = "Label_ReportValue";
        Label_ReportValue.Size = new Size(15, 15);
        Label_ReportValue.TabIndex = 11;
        Label_ReportValue.Text = "0";
        Label_ReportValue.TextAlign = ContentAlignment.TopRight;
        // 
        // Label_Request
        // 
        Label_Request.AutoSize = true;
        Label_Request.Location = new Point(16, 167);
        Label_Request.Name = "Label_Request";
        Label_Request.Size = new Size(60, 15);
        Label_Request.TabIndex = 12;
        Label_Request.Text = "Request";
        // 
        // Label_RequestValue
        // 
        Label_RequestValue.AutoSize = true;
        Label_RequestValue.Location = new Point(230, 167);
        Label_RequestValue.Name = "Label_RequestValue";
        Label_RequestValue.Size = new Size(15, 15);
        Label_RequestValue.TabIndex = 13;
        Label_RequestValue.Text = "0";
        Label_RequestValue.TextAlign = ContentAlignment.TopRight;
        // 
        // Label_BreakOut
        // 
        Label_BreakOut.AutoSize = true;
        Label_BreakOut.Location = new Point(16, 189);
        Label_BreakOut.Name = "Label_BreakOut";
        Label_BreakOut.Size = new Size(70, 15);
        Label_BreakOut.TabIndex = 14;
        Label_BreakOut.Text = "Break Out";
        // 
        // Label_BreakOutValue
        // 
        Label_BreakOutValue.AutoSize = true;
        Label_BreakOutValue.Location = new Point(230, 189);
        Label_BreakOutValue.Name = "Label_BreakOutValue";
        Label_BreakOutValue.Size = new Size(15, 15);
        Label_BreakOutValue.TabIndex = 15;
        Label_BreakOutValue.Text = "0";
        Label_BreakOutValue.TextAlign = ContentAlignment.TopRight;
        // 
        // Label_Pattern
        // 
        Label_Pattern.AutoSize = true;
        Label_Pattern.Location = new Point(16, 211);
        Label_Pattern.Name = "Label_Pattern";
        Label_Pattern.Size = new Size(53, 15);
        Label_Pattern.TabIndex = 16;
        Label_Pattern.Text = "Pattern";
        // 
        // Label_PatternValue
        // 
        Label_PatternValue.AutoSize = true;
        Label_PatternValue.Location = new Point(230, 211);
        Label_PatternValue.Name = "Label_PatternValue";
        Label_PatternValue.Size = new Size(15, 15);
        Label_PatternValue.TabIndex = 17;
        Label_PatternValue.Text = "0";
        Label_PatternValue.TextAlign = ContentAlignment.TopRight;
        // 
        // Label_StraightIn
        // 
        Label_StraightIn.AutoSize = true;
        Label_StraightIn.Location = new Point(16, 233);
        Label_StraightIn.Name = "Label_StraightIn";
        Label_StraightIn.Size = new Size(73, 15);
        Label_StraightIn.TabIndex = 18;
        Label_StraightIn.Text = "Straight In";
        // 
        // Label_StraightInValue
        // 
        Label_StraightInValue.AutoSize = true;
        Label_StraightInValue.Location = new Point(230, 233);
        Label_StraightInValue.Name = "Label_StraightInValue";
        Label_StraightInValue.Size = new Size(15, 15);
        Label_StraightInValue.TabIndex = 19;
        Label_StraightInValue.Text = "0";
        Label_StraightInValue.TextAlign = ContentAlignment.TopRight;
        // 
        // Label_CategoryArrival
        // 
        Label_CategoryArrival.AutoSize = true;
        Label_CategoryArrival.Location = new Point(300, 57);
        Label_CategoryArrival.Name = "Label_CategoryArrival";
        Label_CategoryArrival.Size = new Size(136, 15);
        Label_CategoryArrival.TabIndex = 20;
        Label_CategoryArrival.Text = "Category Sep Arrival";
        // 
        // Label_CategoryArrivalValue
        // 
        Label_CategoryArrivalValue.AutoSize = true;
        Label_CategoryArrivalValue.Location = new Point(515, 57);
        Label_CategoryArrivalValue.Name = "Label_CategoryArrivalValue";
        Label_CategoryArrivalValue.Size = new Size(15, 15);
        Label_CategoryArrivalValue.TabIndex = 21;
        Label_CategoryArrivalValue.Text = "0";
        Label_CategoryArrivalValue.TextAlign = ContentAlignment.TopRight;
        // 
        // Label_RunwaySeparation
        // 
        Label_RunwaySeparation.AutoSize = true;
        Label_RunwaySeparation.Location = new Point(300, 79);
        Label_RunwaySeparation.Name = "Label_RunwaySeparation";
        Label_RunwaySeparation.Size = new Size(131, 15);
        Label_RunwaySeparation.TabIndex = 22;
        Label_RunwaySeparation.Text = "Runway Separation";
        // 
        // Label_RunwaySeparationValue
        // 
        Label_RunwaySeparationValue.AutoSize = true;
        Label_RunwaySeparationValue.Location = new Point(515, 79);
        Label_RunwaySeparationValue.Name = "Label_RunwaySeparationValue";
        Label_RunwaySeparationValue.Size = new Size(15, 15);
        Label_RunwaySeparationValue.TabIndex = 23;
        Label_RunwaySeparationValue.Text = "0";
        Label_RunwaySeparationValue.TextAlign = ContentAlignment.TopRight;
        // 
        // Label_CategoryDeparture
        // 
        Label_CategoryDeparture.AutoSize = true;
        Label_CategoryDeparture.Location = new Point(300, 101);
        Label_CategoryDeparture.Name = "Label_CategoryDeparture";
        Label_CategoryDeparture.Size = new Size(160, 15);
        Label_CategoryDeparture.TabIndex = 24;
        Label_CategoryDeparture.Text = "Category Sep Departure";
        // 
        // Label_CategoryDepartureValue
        // 
        Label_CategoryDepartureValue.AutoSize = true;
        Label_CategoryDepartureValue.Location = new Point(515, 101);
        Label_CategoryDepartureValue.Name = "Label_CategoryDepartureValue";
        Label_CategoryDepartureValue.Size = new Size(15, 15);
        Label_CategoryDepartureValue.TabIndex = 25;
        Label_CategoryDepartureValue.Text = "0";
        Label_CategoryDepartureValue.TextAlign = ContentAlignment.TopRight;
        // 
        // Label_Crashes
        // 
        Label_Crashes.AutoSize = true;
        Label_Crashes.Location = new Point(300, 123);
        Label_Crashes.Name = "Label_Crashes";
        Label_Crashes.Size = new Size(59, 15);
        Label_Crashes.TabIndex = 26;
        Label_Crashes.Text = "Crashes";
        // 
        // Label_CrashesValue
        // 
        Label_CrashesValue.AutoSize = true;
        Label_CrashesValue.Location = new Point(515, 123);
        Label_CrashesValue.Name = "Label_CrashesValue";
        Label_CrashesValue.Size = new Size(15, 15);
        Label_CrashesValue.TabIndex = 27;
        Label_CrashesValue.Text = "0";
        Label_CrashesValue.TextAlign = ContentAlignment.TopRight;
        // 
        // Label_Rating
        // 
        Label_Rating.AutoSize = true;
        Label_Rating.Location = new Point(300, 235);
        Label_Rating.Name = "Label_Rating";
        Label_Rating.Size = new Size(86, 15);
        Label_Rating.TabIndex = 28;
        Label_Rating.Text = "Your Rating:";
        // 
        // TextBox_Rating
        // 
        TextBox_Rating.Location = new Point(465, 231);
        TextBox_Rating.Name = "TextBox_Rating";
        TextBox_Rating.ReadOnly = true;
        TextBox_Rating.Size = new Size(90, 21);
        TextBox_Rating.TabIndex = 29;
        TextBox_Rating.Text = "0";
        TextBox_Rating.TextAlign = HorizontalAlignment.Right;
        // 
        // GroupBox_Profile
        // 
        GroupBox_Profile.Controls.Add(Label_ProfileSummary);
        GroupBox_Profile.Controls.Add(ListBox_Rewards);
        GroupBox_Profile.Location = new Point(16, 106);
        GroupBox_Profile.Name = "GroupBox_Profile";
        GroupBox_Profile.Size = new Size(580, 280);
        GroupBox_Profile.TabIndex = 2;
        GroupBox_Profile.TabStop = false;
        GroupBox_Profile.Text = " Controller History ";
        GroupBox_Profile.Visible = false;
        // 
        // Label_ProfileSummary
        // 
        Label_ProfileSummary.Location = new Point(16, 24);
        Label_ProfileSummary.Name = "Label_ProfileSummary";
        Label_ProfileSummary.Size = new Size(540, 94);
        Label_ProfileSummary.TabIndex = 0;
        Label_ProfileSummary.Text = "Sessions: 0\nAverage rating: 0";
        // 
        // ListBox_Rewards
        // 
        ListBox_Rewards.Location = new Point(16, 125);
        ListBox_Rewards.Name = "ListBox_Rewards";
        ListBox_Rewards.Size = new Size(540, 124);
        ListBox_Rewards.TabIndex = 1;
        // 
        // Label_Status
        // 
        Label_Status.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
        Label_Status.Location = new Point(16, 400);
        Label_Status.Name = "Label_Status";
        Label_Status.Size = new Size(580, 36);
        Label_Status.TabIndex = 3;
        Label_Status.Text = "Current session";
        // 
        // Button_OK
        // 
        Button_OK.BackColor = SystemColors.ButtonFace;
        Button_OK.DialogResult = DialogResult.OK;
        Button_OK.Location = new Point(486, 20);
        Button_OK.Name = "Button_OK";
        Button_OK.Size = new Size(110, 28);
        Button_OK.TabIndex = 0;
        Button_OK.Text = "OK";
        Button_OK.UseVisualStyleBackColor = false;
        // 
        // Button_Cancel
        // 
        Button_Cancel.BackColor = SystemColors.ButtonFace;
        Button_Cancel.DialogResult = DialogResult.Cancel;
        Button_Cancel.Location = new Point(486, 58);
        Button_Cancel.Name = "Button_Cancel";
        Button_Cancel.Size = new Size(110, 28);
        Button_Cancel.TabIndex = 1;
        Button_Cancel.Text = "Cancel";
        Button_Cancel.UseVisualStyleBackColor = false;
        // 
        // Form_Performance
        // 
        AcceptButton = Button_OK;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = SystemColors.Window;
        CancelButton = Button_Cancel;
        ClientSize = new Size(614, 448);
        Controls.Add(GroupBox_Airport);
        Controls.Add(GroupBox_Scenario);
        Controls.Add(GroupBox_Profile);
        Controls.Add(Label_Status);
        Controls.Add(Button_OK);
        Controls.Add(Button_Cancel);
        Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "Form_Performance";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Performance Review";
        Load += Form_ControllerStats_Load;
        GroupBox_Airport.ResumeLayout(false);
        GroupBox_Airport.PerformLayout();
        GroupBox_Scenario.ResumeLayout(false);
        GroupBox_Scenario.PerformLayout();
        GroupBox_Profile.ResumeLayout(false);
        ResumeLayout(false);
    }
}
