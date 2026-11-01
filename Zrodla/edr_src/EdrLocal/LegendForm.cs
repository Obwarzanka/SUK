using System.Drawing;
using System.Windows.Forms;

namespace EdrLocal;

public sealed class LegendForm : ReferenceForm
{
	private static readonly Font NormalText = new Font("Tahoma", 8.25f);

	private static readonly Font BoldText = new Font("Tahoma", 8.25f, FontStyle.Bold);

	public LegendForm()
	{
		Text = "Legenda";
		Font = NormalText;
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MinimizeBox = (MaximizeBox = false);
		ShowInTaskbar = false;
		StartPosition = FormStartPosition.CenterParent;
		ClientSize = new Size(1000, 610);
		Padding = new Padding(8);
		BackColor = Color.White;
		Panel panel = new Panel
		{
			Dock = DockStyle.Bottom,
			Height = 35
		};
		SilverButton silverButton = new SilverButton
		{
			Text = "OK",
			Dock = DockStyle.Right,
			Width = 90,
			Font = NormalText,
			DialogResult = DialogResult.OK
		};
		panel.Controls.Add(silverButton);
		AcceptButton = (CancelButton = silverButton);
		Controls.Add(panel);
		Panel panel2 = new Panel
		{
			Dock = DockStyle.Fill,
			AutoScroll = true
		};
		Controls.Add(panel2);
		panel2.BringToFront();
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Top,
			AutoSize = true,
			ColumnCount = 1,
			Padding = Padding.Empty,
			Margin = Padding.Empty
		};
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		panel2.Controls.Add(tableLayoutPanel);
		GroupBox groupBox = Group(tableLayoutPanel, "Znaczenie kolorów", 322);
		TableLayoutPanel tableLayoutPanel2 = Table(groupBox, 3, 1);
		for (int i = 0; i < 3; i++)
		{
			tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
		}
		TableLayoutPanel tableLayoutPanel3 = Rows();
		TableLayoutPanel tableLayoutPanel4 = Rows();
		TableLayoutPanel tableLayoutPanel5 = Rows();
		tableLayoutPanel2.Controls.Add(tableLayoutPanel3, 0, 0);
		tableLayoutPanel2.Controls.Add(tableLayoutPanel4, 1, 0);
		tableLayoutPanel2.Controls.Add(tableLayoutPanel5, 2, 0);
		Item(tableLayoutPanel3, "K", Color.FromArgb(75, 0, 75), Color.White, "Kursuje");
		Item(tableLayoutPanel3, "K", Color.FromArgb(255, 83, 83), Color.Black, "Pociąg posiada uwagi eksploatacyjne");
		Item(tableLayoutPanel3, "NK", Color.FromArgb(75, 0, 75), Color.White, "Nie kursuje");
		Item(tableLayoutPanel3, "NK", Color.Blue, Color.White, "Rezygnacja z trasy przewoźnika (nie uruchamiać; oznaczenie archiwalne)");
		Item(tableLayoutPanel3, "NK", Color.Teal, Color.White, "Odwołanie pociągu w zamknięciu (nie uruchamiać; oznaczenie archiwalne)");
		Item(tableLayoutPanel3, "+/-", Color.Maroon, Color.White, "Opóźnienie");
		Item(tableLayoutPanel3, "+/-", EdrPalette.Quality, Color.Black, "Pociąg objęty rozliczeniem z tytułu JAKOŚCI");
		Item(tableLayoutPanel4, "Rodz.", Color.FromArgb(180, 180, 180), Color.White, "Czas przejazdu potwierdzony przez dyspozytora");
		Item(tableLayoutPanel4, "Rodz.", Color.Maroon, Color.White, "Czas przejazdu potwierdzony przez urządzenie");
		Item(tableLayoutPanel4, "Rodz.", Color.Green, Color.White, "Czas przejazdu potwierdzony przez ISDR");
		Item(tableLayoutPanel4, "Nr pociągu", Color.White, Color.Black, "Pociągi intermodalne z prędkościami pasażerskimi do 120 km/h", Color.Red);
		Item(tableLayoutPanel4, "Nr pociągu*", Color.White, Color.Black, "Pociągi zdążające i/lub oczekujące na skomunikowanie");
		Item(tableLayoutPanel4, "Nr pociągu", Color.Yellow, Color.Black, "Przesyłki nadzwyczajne");
		Item(tableLayoutPanel5, "Z kierunku post.", EdrPalette.Dangerous, Color.White, "Pociąg aktualnie przewozi Towary Niebezpieczne (TN)");
		Item(tableLayoutPanel5, "W kierunku post.", EdrPalette.HighRisk, Color.Black, "Pociąg przewozi Towary Wysokiego Ryzyka (TWR)");
		Item(tableLayoutPanel5, "Typ post.", Color.FromArgb(240, 251, 255), Color.Black, "Postój handlowy na danej stacji");
		Item(tableLayoutPanel5, "Odjazd rzecz.", Color.Pink, Color.Black, "Postój pociągu (POCIĄG STÓJ)");
		Item(tableLayoutPanel5, "Odjazd rzecz.", Color.Orange, Color.Black, "Postój pociągu z wprowadzoną prognozą odjazdu (POCIĄG STÓJ z prognozą)");
		Item(tableLayoutPanel5, "Przewoźnik", Color.Yellow, Color.Black, "Pociąg pasażerski posiadający uwagi przewoźnika");
		GroupBox groupBox2 = Group(tableLayoutPanel, "Kolor paska zaznaczenia - tzw. 'wstęga'", 66);
		TableLayoutPanel tableLayoutPanel6 = Table(groupBox2, 4, 1);
		for (int j = 0; j < 4; j++)
		{
			tableLayoutPanel6.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
		}
		Band(tableLayoutPanel6, 0, Color.Magenta, "Przyjazd poc.\n(kończy bieg)");
		Band(tableLayoutPanel6, 1, Color.Lime, "Odjazd poc.\n(rozpoczyna bieg)");
		Band(tableLayoutPanel6, 2, Color.Cyan, "Przejazd poc. bez zatrzymania");
		Band(tableLayoutPanel6, 3, Color.FromArgb(51, 153, 255), "Przejazd poc. z postojem");
		GroupBox groupBox3 = Group(tableLayoutPanel, "Oznaczenie typu postoju", 77);
		TableLayoutPanel tableLayoutPanel7 = Table(groupBox3, 3, 2);
		for (int k = 0; k < 3; k++)
		{
			tableLayoutPanel7.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
		}
		string[] array = new string[6] { "ph", "pm", "zp", "pn", "zd", "pt" };
		string[] array2 = new string[6] { "postój handlowy", "postój dla prac manewrowych", "zmiana pracownicza", "postój niehandlowy", "zmiana drużyny", "postój techniczny" };
		for (int l = 0; l < 6; l++)
		{
			tableLayoutPanel7.Controls.Add(new Label
			{
				Text = array[l] + "     " + array2[l],
				Dock = DockStyle.Fill,
				TextAlign = ContentAlignment.MiddleLeft,
				Font = NormalText
			}, l % 3, l / 3);
		}
		GroupBox groupBox4 = Group(tableLayoutPanel, "Dzienniki ruchu i tory stacyjne", 62);
		TableLayoutPanel tableLayoutPanel8 = Table(groupBox4, 4, 1);
		for (int m = 0; m < 4; m++)
		{
			tableLayoutPanel8.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
		}
		Band(tableLayoutPanel8, 0, EdrPalette.Required, "Pole oczekuje na uzupełnienie");
		Band(tableLayoutPanel8, 1, Color.FromArgb(25, 134, 18), "Tor wolny");
		Band(tableLayoutPanel8, 2, Color.Orange, "Tor zarezerwowany");
		Band(tableLayoutPanel8, 3, Color.Firebrick, "Tor zajęty");
		int num = Screen.FromControl(this).WorkingArea.Height - 80;
		if (Height > num)
		{
			Height = num;
		}
	}

	private static GroupBox Group(TableLayoutPanel parent, string title, int height)
	{
		int row = parent.RowCount++;
		parent.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
		GroupBox groupBox = new GroupBox();
		groupBox.Text = title;
		groupBox.Font = BoldText;
		groupBox.Dock = DockStyle.Fill;
		groupBox.Padding = new Padding(7, 4, 7, 5);
		groupBox.Margin = new Padding(0, 0, 0, 5);
		GroupBox groupBox2 = groupBox;
		parent.Controls.Add(groupBox2, 0, row);
		return groupBox2;
	}

	private static TableLayoutPanel Table(Control parent, int columns, int rows)
	{
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
		tableLayoutPanel.Dock = DockStyle.Fill;
		tableLayoutPanel.ColumnCount = columns;
		tableLayoutPanel.RowCount = rows;
		tableLayoutPanel.Margin = Padding.Empty;
		tableLayoutPanel.Padding = Padding.Empty;
		TableLayoutPanel tableLayoutPanel2 = tableLayoutPanel;
		for (int i = 0; i < rows; i++)
		{
			tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / (float)rows));
		}
		parent.Controls.Add(tableLayoutPanel2);
		return tableLayoutPanel2;
	}

	private static TableLayoutPanel Rows()
	{
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
		tableLayoutPanel.Dock = DockStyle.Top;
		tableLayoutPanel.AutoSize = true;
		tableLayoutPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		tableLayoutPanel.ColumnCount = 2;
		tableLayoutPanel.RowCount = 0;
		tableLayoutPanel.Margin = Padding.Empty;
		TableLayoutPanel tableLayoutPanel2 = tableLayoutPanel;
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108f));
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		return tableLayoutPanel2;
	}

	private static void Item(TableLayoutPanel table, string title, Color back, Color ink, string text)
	{
		Item(table, title, back, ink, text, Color.Transparent);
	}

	private static void Item(TableLayoutPanel table, string title, Color back, Color ink, string text, Color border)
	{
		int row = table.RowCount++;
		table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
		Label sample = new Label
		{
			Text = title,
			BackColor = back,
			ForeColor = ink,
			Font = BoldText,
			TextAlign = ContentAlignment.MiddleCenter,
			Dock = DockStyle.Fill,
			Margin = new Padding(0, 6, 6, 6),
			Tag = "LegendSample"
		};
		if (border != Color.Transparent)
		{
			sample.Paint += (object s, PaintEventArgs e) =>
			{
				using Pen pen = new Pen(border, 3f);
				e.Graphics.DrawRectangle(pen, 1, 1, sample.Width - 3, sample.Height - 3);
			};
		}
		table.Controls.Add(sample, 0, row);
		table.Controls.Add(new Label
		{
			Text = text,
			Font = NormalText,
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft,
			Margin = new Padding(1, 0, 6, 0)
		}, 1, row);
	}

	private static void Band(TableLayoutPanel table, int col, Color colour, string text)
	{
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
		tableLayoutPanel.Dock = DockStyle.Fill;
		tableLayoutPanel.ColumnCount = 2;
		tableLayoutPanel.Margin = Padding.Empty;
		TableLayoutPanel tableLayoutPanel2 = tableLayoutPanel;
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 67f));
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel2.Controls.Add(new Label
		{
			BackColor = colour,
			Dock = DockStyle.Fill,
			Margin = new Padding(0, 8, 5, 8),
			Tag = "LegendSample"
		}, 0, 0);
		tableLayoutPanel2.Controls.Add(new Label
		{
			Text = text,
			Dock = DockStyle.Fill,
			Font = NormalText,
			TextAlign = ContentAlignment.MiddleLeft
		}, 1, 0);
		table.Controls.Add(tableLayoutPanel2, col, 0);
	}
}
