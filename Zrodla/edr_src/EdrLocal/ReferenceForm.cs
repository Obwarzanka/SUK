using System.Drawing;
using System.Windows.Forms;

namespace EdrLocal;

public class ReferenceForm : Form
{
	public ReferenceForm()
	{
		FormBorderStyle = FormBorderStyle.Sizable;
		AutoScaleMode = AutoScaleMode.None;
		BackColor = Color.FromArgb(240, 240, 240);
		Padding = new Padding(3);
		DoubleBuffered = true;
		Icon = ReferenceTheme.ApplicationIcon;
	}
}
