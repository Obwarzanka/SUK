using System;
using System.Windows.Forms;

namespace EdrLocal;

public sealed class StationTrackCell : DataGridViewTextBoxCell
{
	public Engine Engine { get; set; }

	public override Type EditType => typeof(StationTrackEditingControl);

	public override object Clone()
	{
		StationTrackCell stationTrackCell = (StationTrackCell)base.Clone();
		stationTrackCell.Engine = Engine;
		return stationTrackCell;
	}

	public override void InitializeEditingControl(int rowIndex, object value, DataGridViewCellStyle style)
	{
		base.InitializeEditingControl(rowIndex, value, style);
		StationTrackEditingControl stationTrackEditingControl = (StationTrackEditingControl)DataGridView.EditingControl;
		stationTrackEditingControl.Reset(Engine, Convert.ToString(Value));
	}
}
