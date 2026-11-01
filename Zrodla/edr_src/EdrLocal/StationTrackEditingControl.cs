using System;
using System.Drawing;
using System.Windows.Forms;

namespace EdrLocal;

public sealed class StationTrackEditingControl : StationTrackComboBox, IDataGridViewEditingControl
{
	private bool resetting;

	public object EditingControlFormattedValue
	{
		get
		{
			return Text;
		}
		set
		{
			Text = Convert.ToString(value);
		}
	}

	public int EditingControlRowIndex { get; set; }

	public bool EditingControlValueChanged { get; set; }

	public DataGridView EditingControlDataGridView { get; set; }

	public bool RepositionEditingControlOnValueChange => false;

	public Cursor EditingPanelCursor => Cursors.Default;

	public StationTrackEditingControl()
	{
		DropDownStyle = ComboBoxStyle.DropDown;
	}

	internal void Reset(Engine source, string selected)
	{
		resetting = true;
		try
		{
			Configure(source, selected);
			EditingControlValueChanged = false;
			EditingControlDataGridView.NotifyCurrentCellDirty(dirty: false);
		}
		finally
		{
			resetting = false;
		}
		if (!EditingControlDataGridView.IsHandleCreated)
		{
			return;
		}
		EditingControlDataGridView.BeginInvoke((Action)(() =>
		{
			if (!IsDisposed && Visible && EditingControlDataGridView != null && EditingControlDataGridView.IsCurrentCellInEditMode && EditingControlDataGridView.EditingControl == this)
			{
				DroppedDown = true;
			}
		}));
	}

	protected override void OnSelectedIndexChanged(EventArgs e)
	{
		base.OnSelectedIndexChanged(e);
		if (!resetting && EditingControlDataGridView != null && !EditingControlDataGridView.Disposing && !EditingControlDataGridView.IsDisposed)
		{
			EditingControlValueChanged = true;
			EditingControlDataGridView.NotifyCurrentCellDirty(dirty: true);
		}
	}

	protected override void OnTextChanged(EventArgs e)
	{
		base.OnTextChanged(e);
		if (!resetting && EditingControlDataGridView != null && !EditingControlDataGridView.Disposing && !EditingControlDataGridView.IsDisposed)
		{
			EditingControlValueChanged = true;
			EditingControlDataGridView.NotifyCurrentCellDirty(dirty: true);
		}
	}

	public object GetEditingControlFormattedValue(DataGridViewDataErrorContexts context)
	{
		return Text;
	}

	public void ApplyCellStyleToEditingControl(DataGridViewCellStyle style)
	{
		Font = style.Font;
		BackColor = SystemColors.Window;
		ForeColor = SystemColors.WindowText;
	}

	public void PrepareEditingControlForEdit(bool selectAll)
	{
		if (selectAll)
		{
			SelectAll();
		}
	}

	public bool EditingControlWantsInputKey(Keys keyData, bool gridWants)
	{
		Keys keys = keyData & Keys.KeyCode;
		if (keys != Keys.Left && keys != Keys.Right && keys != Keys.Back && keys != Keys.Delete && keys != Keys.Up && keys != Keys.Down && keys != Keys.Home && keys != Keys.End && keys != Keys.Prior && keys != Keys.Next && keys != Keys.Space && keys != Keys.F4)
		{
			return !gridWants;
		}
		return true;
	}
}
