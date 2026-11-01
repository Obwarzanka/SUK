using System;
using System.Windows.Forms;

namespace EdrLocal;

public class ReferenceListBox : ListBox
{
	private int remainder;

	protected override void OnMouseWheel(MouseEventArgs e)
	{
		if (e is HandledMouseEventArgs e2)
		{
			e2.Handled = true;
		}
		remainder += e.Delta;
		int num = remainder / SystemInformation.MouseWheelScrollDelta;
		remainder %= SystemInformation.MouseWheelScrollDelta;
		if (num != 0 && Items.Count > 0)
		{
			SelectedIndex = ((SelectedIndex >= 0) ? Math.Max(0, Math.Min(Items.Count - 1, SelectedIndex - num)) : 0);
		}
	}
}
