using System;
using System.Windows.Forms;

namespace EdrLocal;

public sealed class ClockPartUpDown : NumericUpDown
{
	private int remainder;

	public event Action<int> WheelStep;

	protected override void OnMouseWheel(MouseEventArgs e)
	{
		if (Enabled && !ReadOnly)
		{
			if (e is HandledMouseEventArgs e2)
			{
				e2.Handled = true;
			}
			int num = ClockInteraction.Steps(ref remainder, e.Delta);
			if (num != 0 && WheelStep != null)
			{
				WheelStep(num);
			}
		}
	}
}
