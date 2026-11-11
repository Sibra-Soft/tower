namespace Tower.Forms
{
    public partial class Form_ButtonBar : Form
    {
        public event EventHandler<string>? ButtonClicked;

        private readonly List<string> buttons = [];

        /// <summary>
        /// Generate the buttonbar based on the images
        /// </summary>
        private void GenerateButtonBar()
        {
            FlowLayoutPanel_Buttons.SuspendLayout();
            FlowLayoutPanel_Buttons.Controls.Clear();

            FlowLayoutPanel_Buttons.FlowDirection = FlowDirection.TopDown;
            FlowLayoutPanel_Buttons.WrapContents = false;
            FlowLayoutPanel_Buttons.AutoSize = true;
            FlowLayoutPanel_Buttons.Padding = Padding.Empty;

            foreach(var button in buttons)
            {
                var panelButton = new Panel
                {
                    Size = new Size(32, 32),
                    Margin = Padding.Empty,
                    Name = button,
                    BackgroundImage = ImageList_Buttons.Images[button + "UP"],
                    BackgroundImageLayout = ImageLayout.Stretch,
                };

                panelButton.MouseDown += (sender, e) =>
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        panelButton.BackgroundImage = ImageList_Buttons.Images[button + "DN"];
                    }
                };

                panelButton.MouseUp += (sender, e) =>
                {
                    panelButton.BackgroundImage = ImageList_Buttons.Images[button + "UP"];
                };

                panelButton.Click += (sender, e) =>
                {
                    ButtonClicked?.Invoke(this, button);
                };

                FlowLayoutPanel_Buttons.Controls.Add(panelButton);
            }
            
            FlowLayoutPanel_Buttons.ResumeLayout(true);

            ClientSize = new Size(
                32,
                FlowLayoutPanel_Buttons.PreferredSize.Height
            );
        }

        public Form_ButtonBar()
        {
            InitializeComponent();

            buttons.Add("RADIO");
            buttons.Add("COFFEE");
            buttons.Add("COMPASS");
            buttons.Add("LIGHTS");
            buttons.Add("ASDE");
            buttons.Add("BRITE");
            buttons.Add("STRIPS");
            buttons.Add("PERKS");
            buttons.Add("EXIT");

            GenerateButtonBar();
        }
    }
}
