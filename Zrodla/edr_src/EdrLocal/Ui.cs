using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace EdrLocal;

public static class Ui
{
	public static bool Testing;

	public static Color Cream = EdrPalette.Cream;

	public static Color Blue = Color.FromArgb(202, 235, 250);

	public static Color Green = Color.FromArgb(215, 235, 202);

	public static void Run(IWin32Window owner, Action action)
	{
		try
		{
			action();
		}
		catch (Exception ex)
		{
			if (Testing)
			{
				throw;
			}
			MessageBox.Show(owner, ex.Message, (ex is TrainTimeException) ? "Komunikat" : "EDR - informacja", MessageBoxButtons.OK, (ex is TrainTimeException) ? MessageBoxIcon.Hand : MessageBoxIcon.Asterisk);
		}
	}

	public static Button Button(string text, Action action)
	{
		SilverButton b = new SilverButton
		{
			Text = text,
			AutoSize = true,
			MinimumSize = new Size(90, 28),
			Margin = new Padding(4)
		};
		b.Click += (object s, EventArgs e) =>
		{
			Run(b, action);
		};
		return b;
	}

	public static ToolStripButton Tool(string text, string hint, Action action)
	{
		ToolStripButton b = new ToolStripButton(text)
		{
			ToolTipText = hint,
			DisplayStyle = ToolStripItemDisplayStyle.Text
		};
		b.Click += (object s, EventArgs e) =>
		{
			Run(b.GetCurrentParent(), action);
		};
		return b;
	}

	public static ToolStripMenuItem Menu(string text, Action action)
	{
		ToolStripMenuItem i = new ToolStripMenuItem(text);
		i.Click += (object s, EventArgs e) =>
		{
			Run(i.GetCurrentParent(), action);
		};
		return i;
	}

	public static DataGridView Grid(bool journal)
	{
		ReferenceGrid referenceGrid = new ReferenceGrid();
		referenceGrid.Dock = DockStyle.Fill;
		referenceGrid.AllowUserToAddRows = false;
		referenceGrid.AllowUserToDeleteRows = false;
		referenceGrid.AllowUserToResizeRows = false;
		referenceGrid.ReadOnly = true;
		referenceGrid.MultiSelect = false;
		referenceGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
		referenceGrid.BackgroundColor = Color.White;
		referenceGrid.BorderStyle = BorderStyle.Fixed3D;
		referenceGrid.RowHeadersVisible = false;
		referenceGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
		referenceGrid.EnableHeadersVisualStyles = false;
		referenceGrid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
		referenceGrid.Font = new Font("Tahoma", 8.25f);
		referenceGrid.ColumnHeadersHeight = (journal ? 43 : 31);
		referenceGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
		ReferenceGrid referenceGrid2 = referenceGrid;
		referenceGrid2.RowTemplate.Height = 22;
		referenceGrid2.ColumnHeadersDefaultCellStyle.BackColor = (journal ? Color.FromArgb(66, 66, 66) : Color.FromArgb(228, 228, 228));
		referenceGrid2.ColumnHeadersDefaultCellStyle.ForeColor = (journal ? Color.White : Color.Black);
		referenceGrid2.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
		referenceGrid2.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 166, 204);
		referenceGrid2.DefaultCellStyle.SelectionForeColor = Color.Black;
		referenceGrid2.DefaultCellStyle.NullValue = "";
		referenceGrid2.CellBorderStyle = DataGridViewCellBorderStyle.Single;
		referenceGrid2.GridColor = Color.FromArgb(215, 215, 215);
		referenceGrid2.DataError += (object s, DataGridViewDataErrorEventArgs e) =>
		{
			e.ThrowException = false;
		};
		return referenceGrid2;
	}

	public static void Column(DataGridView g, string name, string title, int width)
	{
		g.Columns.Add(new DataGridViewTextBoxColumn
		{
			Name = name,
			HeaderText = title,
			Width = width,
			SortMode = DataGridViewColumnSortMode.NotSortable
		});
	}

	public static string Time(DateTime? t)
	{
		if (!t.HasValue)
		{
			return "";
		}
		return t.Value.ToString("HH:mm");
	}

	public static string FullTime(DateTime? t)
	{
		if (!t.HasValue)
		{
			return "";
		}
		return t.Value.ToString("dd.MM.yyyy HH:mm");
	}

	public static string PlatformText(string value)
	{
		string text = (value ?? "").Trim();
		if (!int.TryParse(text, out var result) || result < 1 || result > 3999)
		{
			return text;
		}
		int[] array = new int[13]
		{
			1000, 900, 500, 400, 100, 90, 50, 40, 10, 9,
			5, 4, 1
		};
		string[] array2 = new string[13]
		{
			"M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX",
			"V", "IV", "I"
		};
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < array.Length; i++)
		{
			while (result >= array[i])
			{
				stringBuilder.Append(array2[i]);
				result -= array[i];
			}
		}
		return stringBuilder.ToString();
	}

	public static string PlatformTrackText(string value)
	{
		string text = (value ?? "").Trim();
		int num = text.IndexOf(' ');
		if (num >= 0)
		{
			return PlatformText(text.Substring(0, num)) + text.Substring(num);
		}
		return PlatformText(text);
	}

	public static void AfterFrame(Action action)
	{
		Timer timer = new Timer
		{
			Interval = 350
		};
		timer.Tick += (object s, EventArgs e) =>
		{
			timer.Stop();
			timer.Dispose();
			action();
		};
		timer.Start();
	}

	public static void Capture(Form form, string path)
	{
		form.Refresh();
		using Bitmap bitmap = new Bitmap(form.Width, form.Height);
		form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
		using (Graphics graphics = Graphics.FromImage(bitmap))
		{
			if (form is MainForm mainForm)
			{
				Point point = form.PointToScreen(Point.Empty);
				graphics.TranslateTransform(point.X - form.Left, point.Y - form.Top);
				mainForm.RenderReferenceOverlays(graphics);
			}
		}
		bitmap.Save(path, ImageFormat.Png);
	}

	public static ComboBox Combo(IEnumerable<string> values, string selected, bool editable)
	{
		ComboBox c = new ComboBox
		{
			DropDownStyle = (editable ? ComboBoxStyle.DropDown : ComboBoxStyle.DropDownList),
			Dock = DockStyle.Fill,
			DrawMode = DrawMode.OwnerDrawFixed
		};
		c.DrawItem += (object s, DrawItemEventArgs e) =>
		{
			e.DrawBackground();
			if (e.Index >= 0)
			{
				TextRenderer.DrawText(e.Graphics, Convert.ToString(c.Items[e.Index]), c.Font, e.Bounds, e.ForeColor, TextFormatFlags.VerticalCenter);
			}
			if (e.Index < 0 && c.SelectedItem != null)
			{
				TextRenderer.DrawText(e.Graphics, Convert.ToString(c.SelectedItem), c.Font, e.Bounds, e.ForeColor, TextFormatFlags.VerticalCenter);
			}
			e.DrawFocusRectangle();
		};
		c.Items.AddRange(values.Cast<object>().ToArray());
		if (c.Items.Count > 0)
		{
			c.SelectedIndex = 0;
		}
		if (selected != null)
		{
			c.Text = selected;
		}
		return c;
	}

	public static DateTimePicker Date(DateTime time, bool nullable, bool active)
	{
		DateTimePicker dateTimePicker = new DateTimePicker();
		dateTimePicker.Value = time;
		dateTimePicker.Format = DateTimePickerFormat.Custom;
		dateTimePicker.CustomFormat = "dd.MM.yyyy  HH:mm";
		dateTimePicker.ShowCheckBox = nullable;
		dateTimePicker.Checked = active;
		dateTimePicker.Dock = DockStyle.Fill;
		return dateTimePicker;
	}

	public static DateTime? Nullable(DateTimePicker p)
	{
		if (!p.Checked)
		{
			return null;
		}
		return new DateTime(p.Value.Year, p.Value.Month, p.Value.Day, p.Value.Hour, p.Value.Minute, 0);
	}

	public static TextBox Text(string value, bool multiline)
	{
		TextBox textBox = new TextBox();
		textBox.Text = value ?? "";
		textBox.Multiline = multiline;
		textBox.ScrollBars = (multiline ? ScrollBars.Vertical : ScrollBars.None);
		textBox.Dock = DockStyle.Fill;
		return textBox;
	}

	public static string Required(TextBox t, string name)
	{
		string text = t.Text.Trim();
		if (text.Length == 0)
		{
			throw new Exception("Wypełnij pole: " + name);
		}
		return text;
	}
}
