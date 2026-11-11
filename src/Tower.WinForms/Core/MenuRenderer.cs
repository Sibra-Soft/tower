namespace Tower.Core
{
    public class MenuRenderer : ToolStripSystemRenderer
    {
        public MenuRenderer()
        {
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (e.AffectedBounds == e.ToolStrip.Bounds) return;
            base.OnRenderToolStripBorder(e);
        }
    }
}
