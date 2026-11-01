using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EdrLocal;

public class ReferenceGrid : DataGridView
{
	private int wheelRemainder;

	public bool SelectWithWheel { get; set; }

	public event EventHandler ClockAccepted;

	public ReferenceGrid()
	{
		SelectWithWheel = true;
		DoubleBuffered = true;
	}

	private void PrepareClickedClock()
	{
		if (CurrentCell is StationTrackCell && !ReadOnly && !CurrentCell.ReadOnly)
		{
			if (!IsCurrentCellInEditMode)
			{
				BeginEdit(selectAll: false);
			}
			if (EditingControl is StationTrackEditingControl { Visible: not false } stationTrackEditingControl)
			{
				stationTrackEditingControl.DroppedDown = true;
			}
		}
		else if (CurrentCell is ClockCell && !ReadOnly && !CurrentCell.ReadOnly)
		{
			if (!IsCurrentCellInEditMode)
			{
				BeginEdit(selectAll: false);
			}
			if (EditingControl is ClockEditingControl clockEditingControl && IsCurrentCellInEditMode)
			{
				clockEditingControl.ShowSuggestions();
			}
		}
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		base.OnMouseDown(e);
		HitTestInfo hitTestInfo = HitTest(e.X, e.Y);
		if (e.Button == MouseButtons.Left && hitTestInfo.RowIndex >= 0 && hitTestInfo.ColumnIndex >= 0 && CurrentCell == Rows[hitTestInfo.RowIndex].Cells[hitTestInfo.ColumnIndex])
		{
			PrepareClickedClock();
		}
	}

	protected override void OnCellMouseClick(DataGridViewCellMouseEventArgs e)
	{
		base.OnCellMouseClick(e);
		if (e.Button == MouseButtons.Left && e.RowIndex >= 0 && e.ColumnIndex >= 0 && CurrentCell == Rows[e.RowIndex].Cells[e.ColumnIndex])
		{
			PrepareClickedClock();
		}
	}

	public bool AcceptSuggestedClock()
	{
		if (!(CurrentCell is ClockCell) || ReadOnly || CurrentCell.ReadOnly)
		{
			return false;
		}
		if (!IsCurrentCellInEditMode && !BeginEdit(selectAll: false))
		{
			return false;
		}
		if (!(EditingControl is ClockEditingControl clockEditingControl) || !clockEditingControl.CompleteSuggestion())
		{
			return false;
		}
		if (!EndEdit())
		{
			return false;
		}
		ClockAccepted?.Invoke(this, EventArgs.Empty);
		return true;
	}

	protected override void OnCellMouseDoubleClick(DataGridViewCellMouseEventArgs e)
	{
		base.OnCellMouseDoubleClick(e);
		if (e.Button == MouseButtons.Left && e.RowIndex >= 0 && e.ColumnIndex >= 0 && CurrentCell == Rows[e.RowIndex].Cells[e.ColumnIndex])
		{
			AcceptSuggestedClock();
		}
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		if (e.Modifiers == Keys.None && e.KeyCode == Keys.Return && AcceptSuggestedClock())
		{
			e.SuppressKeyPress = true;
		}
		else
		{
			base.OnKeyDown(e);
		}
	}

	protected override void OnMouseWheel(MouseEventArgs e)
	{
		HitTestInfo hitTestInfo = HitTest(e.X, e.Y);
		if (hitTestInfo.RowIndex >= 0 && hitTestInfo.ColumnIndex >= 0 && Rows[hitTestInfo.RowIndex].Cells[hitTestInfo.ColumnIndex] is ClockCell && !ReadOnly && !Rows[hitTestInfo.RowIndex].Cells[hitTestInfo.ColumnIndex].ReadOnly)
		{
			if (e is HandledMouseEventArgs e2)
			{
				e2.Handled = true;
			}
			DataGridViewCell dataGridViewCell = Rows[hitTestInfo.RowIndex].Cells[hitTestInfo.ColumnIndex];
			if (CurrentCell != dataGridViewCell)
			{
				CurrentCell = dataGridViewCell;
			}
			if (CurrentCell == dataGridViewCell && (IsCurrentCellInEditMode || BeginEdit(selectAll: false)) && EditingControl is ClockEditingControl clockEditingControl)
			{
				Rectangle cellDisplayRectangle = GetCellDisplayRectangle(hitTestInfo.ColumnIndex, hitTestInfo.RowIndex, cutOverflow: false);
				clockEditingControl.AdjustWheel(e.Delta, e.X - cellDisplayRectangle.Left);
			}
			return;
		}
		if (!SelectWithWheel)
		{
			base.OnMouseWheel(e);
			return;
		}
		if (e is HandledMouseEventArgs e3)
		{
			e3.Handled = true;
		}
		wheelRemainder += e.Delta;
		int num = wheelRemainder / SystemInformation.MouseWheelScrollDelta;
		wheelRemainder %= SystemInformation.MouseWheelScrollDelta;
		if (num == 0 || Rows.Count == 0 || Columns.Count == 0)
		{
			return;
		}
		DataGridViewColumn dataGridViewColumn = (from DataGridViewColumn c in Columns
			where c.Visible
			orderby c.DisplayIndex
			select c).FirstOrDefault();
		if (dataGridViewColumn == null)
		{
			return;
		}
		int index = ((CurrentCell == null) ? dataGridViewColumn.Index : CurrentCell.ColumnIndex);
		int num2 = ((CurrentCell == null) ? (-1) : CurrentCell.RowIndex);
		int num3 = ((num <= 0) ? 1 : (-1));
		for (int num4 = 0; num4 < Math.Abs(num); num4++)
		{
			int num5;
			for (num5 = ((num2 >= 0) ? (num2 + num3) : 0); num5 >= 0 && num5 < Rows.Count && (!Rows[num5].Visible || Rows[num5].IsNewRow); num5 += num3)
			{
			}
			if (num5 < 0 || num5 >= Rows.Count)
			{
				break;
			}
			num2 = num5;
		}
		if (num2 >= 0 && (!IsCurrentCellInEditMode || EndEdit()))
		{
			try
			{
				CurrentCell = Rows[num2].Cells[index];
			}
			catch (InvalidOperationException)
			{
				return;
			}
			if (CurrentCell != null && CurrentCell.RowIndex == num2 && SelectionMode == DataGridViewSelectionMode.FullRowSelect)
			{
				Rows[num2].Selected = true;
			}
		}
	}
}
