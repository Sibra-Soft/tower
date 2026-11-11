namespace Tower.Forms
{
    partial class Form_ButtonBar
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form_ButtonBar));
            ImageList_Buttons = new ImageList(components);
            FlowLayoutPanel_Buttons = new FlowLayoutPanel();
            SuspendLayout();
            // 
            // ImageList_Buttons
            // 
            ImageList_Buttons.ColorDepth = ColorDepth.Depth32Bit;
            ImageList_Buttons.ImageStream = (ImageListStreamer)resources.GetObject("ImageList_Buttons.ImageStream");
            ImageList_Buttons.TransparentColor = Color.Transparent;
            ImageList_Buttons.Images.SetKeyName(0, "ASDEUP");
            ImageList_Buttons.Images.SetKeyName(1, "BRITEUP");
            ImageList_Buttons.Images.SetKeyName(2, "COFFEEUP");
            ImageList_Buttons.Images.SetKeyName(3, "COMPASSUP");
            ImageList_Buttons.Images.SetKeyName(4, "EXITUP");
            ImageList_Buttons.Images.SetKeyName(5, "LIGHTSUP");
            ImageList_Buttons.Images.SetKeyName(6, "PERKSUP");
            ImageList_Buttons.Images.SetKeyName(7, "PHONEUP");
            ImageList_Buttons.Images.SetKeyName(8, "RADIOUP");
            ImageList_Buttons.Images.SetKeyName(9, "STRIPSUP");
            ImageList_Buttons.Images.SetKeyName(10, "STRIPSDN");
            ImageList_Buttons.Images.SetKeyName(11, "RADIODN");
            ImageList_Buttons.Images.SetKeyName(12, "PHONEDN");
            ImageList_Buttons.Images.SetKeyName(13, "PERKSDN");
            ImageList_Buttons.Images.SetKeyName(14, "LIGHTSDN");
            ImageList_Buttons.Images.SetKeyName(15, "EXITDN");
            ImageList_Buttons.Images.SetKeyName(16, "COMPASSDN");
            ImageList_Buttons.Images.SetKeyName(17, "COFFEEDN");
            ImageList_Buttons.Images.SetKeyName(18, "BRITEDN");
            ImageList_Buttons.Images.SetKeyName(19, "ASDEDN");
            // 
            // FlowLayoutPanel_Buttons
            // 
            FlowLayoutPanel_Buttons.BackColor = Color.White;
            FlowLayoutPanel_Buttons.FlowDirection = FlowDirection.TopDown;
            FlowLayoutPanel_Buttons.Location = new Point(0, 0);
            FlowLayoutPanel_Buttons.Margin = new Padding(0);
            FlowLayoutPanel_Buttons.Name = "FlowLayoutPanel_Buttons";
            FlowLayoutPanel_Buttons.Size = new Size(32, 111);
            FlowLayoutPanel_Buttons.TabIndex = 0;
            FlowLayoutPanel_Buttons.WrapContents = false;
            // 
            // Form_ButtonBar
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.Lime;
            ClientSize = new Size(120, 284);
            ControlBox = false;
            Controls.Add(FlowLayoutPanel_Buttons);
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "Form_ButtonBar";
            ShowIcon = false;
            ShowInTaskbar = false;
            SizeGripStyle = SizeGripStyle.Hide;
            Text = " ";
            ResumeLayout(false);
        }

        #endregion

        private ImageList ImageList_Buttons;
        private FlowLayoutPanel FlowLayoutPanel_Buttons;
    }
}