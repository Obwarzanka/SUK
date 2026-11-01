using System.Drawing;
using System.Windows.Forms;

namespace EdrLocal;

public static class TimetableConfirmation
{
	public static bool IsConfirmed(Train train)
	{
		if (!(train.SourceProvider == "SimRail") && !(train.SourceProvider == "TD2"))
		{
			if (!train.Arrival.HasValue)
			{
				return train.Departure.HasValue;
			}
			return true;
		}
		return train.ExternalConfirmed;
	}

	public static Color KindColor(Train train)
	{
		if (train.SourceProvider == "TD2")
		{
			if (!train.ExternalConfirmed)
			{
				return Ui.Cream;
			}
			return Color.Green;
		}
		if (train.SourceProvider != "SimRail")
		{
			if (!IsConfirmed(train))
			{
				return Ui.Cream;
			}
			return Color.Green;
		}
		if (!train.ExternalConfirmed)
		{
			return Ui.Cream;
		}
		if (train.ExternalConfirmedBy != 3)
		{
			if (train.ExternalConfirmedBy != 1)
			{
				return Color.Silver;
			}
			return Color.Green;
		}
		return Color.Maroon;
	}

	public static void Apply(DataGridViewRow row, Train train)
	{
		if (!(train.SourceProvider != "SimRail") || !(train.SourceProvider != "TD2"))
		{
			bool externalConfirmed = train.ExternalConfirmed;
			DataGridViewCell dataGridViewCell = row.Cells["kind"];
			dataGridViewCell.Style.BackColor = KindColor(train);
			dataGridViewCell.Style.ForeColor = (externalConfirmed ? Color.White : Color.Black);
			dataGridViewCell.Style.SelectionBackColor = (externalConfirmed ? dataGridViewCell.Style.BackColor : row.DefaultCellStyle.SelectionBackColor);
			dataGridViewCell.Style.SelectionForeColor = (externalConfirmed ? Color.White : row.DefaultCellStyle.SelectionForeColor);
		}
	}
}
