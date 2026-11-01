using System.Drawing;
using System.Windows.Forms;

namespace EdrLocal;

public class ReferenceStripRenderer : ToolStripSystemRenderer
{
	protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
	{
	}

	protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
	{
		if (e.Item is ToolStripButton && ((ToolStripButton)e.Item).DisplayStyle != ToolStripItemDisplayStyle.Image)
		{
			ReferenceTheme.Silver(e.Graphics, new Rectangle(Point.Empty, e.Item.Size), e.Item.Pressed);
		}
	}
}
