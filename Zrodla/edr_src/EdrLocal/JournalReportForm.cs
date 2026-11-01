using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace EdrLocal;

public sealed class JournalReportForm : ReferenceForm
{
	private readonly Engine engine;

	private readonly bool telephone;

	private readonly DateTimePicker from;

	private readonly DateTimePicker to;

	private readonly ComboBox section;

	private readonly CheckBox history;

	private readonly ToolStripTextBox pageNumber;

	private readonly ToolStripLabel pages;

	private readonly ReportCanvas canvas;

	private JournalReport report;

	public JournalReportForm(Engine value, bool tel, DateTime day)
	{
		engine = value;
		telephone = tel;
		Text = (tel ? "Wyciąg z dziennika telefonicznego" : "Wyciąg z dziennika ruchu");
		Font = ReferenceTheme.Dialog;
		ClientSize = new Size(1000, 765);
		MinimumSize = new Size(950, 550);
		StartPosition = FormStartPosition.CenterParent;
		MinimizeBox = false;
		Panel panel = new Panel
		{
			Dock = DockStyle.Top,
			Height = 82,
			BackColor = SystemColors.Control
		};
		Label value2 = new Label
		{
			Text = "Posterunek ruchu:  " + engine.Station.Name.ToUpper(),
			Bounds = new Rectangle(12, 9, 400, 24),
			AutoEllipsis = true
		};
		panel.Controls.Add(value2);
		from = Ui.Date(day, nullable: false, active: true);
		from.Dock = DockStyle.None;
		from.CustomFormat = "yyyy-MM-dd HH:mm";
		from.SetBounds(457, 7, 177, 25);
		to = Ui.Date((DateTime.Now.Date == day) ? DateTime.Now : day.AddDays(1.0).AddMinutes(-1.0), nullable: false, active: true);
		to.Dock = DockStyle.None;
		to.CustomFormat = "yyyy-MM-dd HH:mm";
		to.SetBounds(457, 39, 177, 25);
		panel.Controls.Add(from);
		panel.Controls.Add(to);
		panel.Controls.Add(new Label
		{
			Text = "Od:",
			Bounds = new Rectangle(420, 12, 33, 21)
		});
		panel.Controls.Add(new Label
		{
			Text = "Do:",
			Bounds = new Rectangle(420, 44, 33, 21)
		});
		section = Ui.Combo(engine.Station.Sections.Select((Section x) => (!engine.IsTd2) ? x.Name : (StationSettings.Title(x) + (x.Td2Archived ? " (archiwalny)" : ""))), engine.IsTd2 ? null : engine.Station.Sections.First().Name, editable: false);
		section.Dock = DockStyle.None;
		section.SetBounds(116, 39, 288, 25);
		section.Visible = !tel;
		panel.Controls.Add(section);
		panel.Controls.Add(new Label
		{
			Text = "Dziennik ruchu:",
			Bounds = new Rectangle(12, 43, 104, 21),
			Visible = !tel
		});
		history = new CheckBox
		{
			Text = "Pokaż historię zmian",
			Bounds = new Rectangle(663, 9, 240, 24)
		};
		panel.Controls.Add(history);
		Button button = Ui.Button("Pokaż", ShowReport);
		button.AutoSize = false;
		button.SetBounds(802, 39, 90, 27);
		panel.Controls.Add(button);
		AcceptButton = button;
		ToolStrip toolStrip = new ToolStrip
		{
			Dock = DockStyle.Top,
			Height = 29,
			GripStyle = ToolStripGripStyle.Visible,
			Renderer = new ToolStripSystemRenderer(),
			BackColor = Color.Silver
		};
		if (LocalPersistence.Enabled)
		{
			toolStrip.Items.Add(Tool("Zapisz PDF", () =>
			{
				if (report == null)
				{
					return;
				}
				using SaveFileDialog saveFileDialog = new SaveFileDialog
				{
					Filter = "PDF (*.pdf)|*.pdf",
					FileName = "wyciag-" + (telephone ? "telefoniczny" : "ruchu") + ".pdf"
				};
				if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
				{
					report.Export(saveFileDialog.FileName);
				}
			}));
		}
		toolStrip.Items.Add(Tool("Drukuj", () =>
		{
			if (report == null)
			{
				return;
			}
			using PrintDocument printDocument = report.Printable();
			using PrintDialog printDialog = new PrintDialog
			{
				Document = printDocument,
				UseEXDialog = true
			};
			if (printDialog.ShowDialog(this) == DialogResult.OK)
			{
				printDocument.Print();
			}
		}));
		toolStrip.Items.Add(new ToolStripSeparator());
		toolStrip.Items.Add(Tool("◀", () =>
		{
			MovePage(-1);
		}));
		toolStrip.Items.Add(Tool("▶", () =>
		{
			MovePage(1);
		}));
		pageNumber = new ToolStripTextBox
		{
			Text = "1",
			AutoSize = false,
			Width = 40
		};
		pageNumber.KeyDown += (object s, KeyEventArgs e) =>
		{
			if (e.KeyCode == Keys.Return)
			{
				if (int.TryParse(pageNumber.Text, out var result))
				{
					canvas.Page = result - 1;
					UpdatePages();
				}
				e.SuppressKeyPress = true;
			}
		};
		toolStrip.Items.Add(pageNumber);
		pages = new ToolStripLabel("/ 0");
		toolStrip.Items.Add(pages);
		toolStrip.Items.Add(new ToolStripSeparator());
		ToolStripComboBox zoom = new ToolStripComboBox
		{
			DropDownStyle = ComboBoxStyle.DropDownList,
			AutoSize = false,
			Width = 116
		};
		zoom.Items.AddRange(new object[7] { "Szerokość strony", "50%", "75%", "100%", "125%", "150%", "200%" });
		zoom.SelectedIndex = 0;
		zoom.SelectedIndexChanged += (object s, EventArgs e) =>
		{
			float scale = 0f;
			if (zoom.SelectedIndex > 0)
			{
				scale = float.Parse(zoom.Text.TrimEnd('%'), CultureInfo.InvariantCulture) / 100f;
			}
			canvas.Zoom(scale);
		};
		toolStrip.Items.Add(zoom);
		toolStrip.Items.Add(new ToolStripSeparator());
		ToolStripButton pan = new ToolStripButton("Przesuwanie strony")
		{
			CheckOnClick = true,
			Image = ReportTools.Glyph("hand"),
			DisplayStyle = ToolStripItemDisplayStyle.Image
		};
		pan.CheckedChanged += (object s, EventArgs e) =>
		{
			canvas.Pan = pan.Checked;
			canvas.Cursor = (pan.Checked ? Cursors.Hand : Cursors.Default);
		};
		toolStrip.Items.Add(pan);
		Button button2 = Ui.Button("Zamknij", () =>
		{
			Close();
		});
		button2.AutoSize = false;
		button2.Size = new Size(92, 28);
		button2.DialogResult = DialogResult.Cancel;
		CancelButton = button2;
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Bottom,
			Height = 37,
			FlowDirection = FlowDirection.RightToLeft,
			Padding = new Padding(4)
		};
		flowLayoutPanel.Controls.Add(button2);
		canvas = new ReportCanvas();
		Controls.Add(canvas);
		Controls.Add(toolStrip);
		Controls.Add(panel);
		Controls.Add(flowLayoutPanel);
	}

	private ToolStripButton Tool(string title, Action action)
	{
		ToolStripButton toolStripButton = new ToolStripButton(title);
		toolStripButton.Image = ReportTools.Glyph(title);
		toolStripButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
		ToolStripButton toolStripButton2 = toolStripButton;
		string toolTipText;
		if (title == "◀")
		{
			toolTipText = "Poprzednia strona";
		}
		else
		{
			toolTipText = ((title == "▶") ? "Następna strona" : title);
		}
		toolStripButton2.ToolTipText = toolTipText;
		ToolStripButton toolStripButton3 = toolStripButton;
		toolStripButton3.Click += (object s, EventArgs e) =>
		{
			Ui.Run(this, action);
		};
		return toolStripButton3;
	}

	private void MovePage(int n)
	{
		canvas.Page += n;
		UpdatePages();
	}

	private void UpdatePages()
	{
		pageNumber.Text = (canvas.Page + 1).ToString();
		pages.Text = "/ " + ((report != null) ? report.PageCount : 0);
	}

	public void ShowReport()
	{
		Ui.Run(this, () =>
		{
			if (to.Value < from.Value)
			{
				throw new Exception("Nieprawidłowy zakres dat.");
			}
			Section sec = (engine.IsTd2 ? engine.Station.Sections.ElementAtOrDefault(section.SelectedIndex) : engine.Station.Sections.FirstOrDefault((Section x) => x.Name == section.Text));
			List<JournalEntry> entries = engine.State.Entries.Where((JournalEntry x) => x.StationId == engine.StationId && (telephone ? (x.SectionId == "TEL") : (x.SectionId == sec.Id)) && x.Time >= from.Value && x.Time <= to.Value).ToList();
			JournalReport journalReport = new JournalReport(engine, entries, telephone, history.Checked, sec, from.Value, to.Value);
			canvas.Report = journalReport;
			if (report != null)
			{
				report.Dispose();
			}
			report = journalReport;
			UpdatePages();
		});
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && report != null)
		{
			report.Dispose();
			report = null;
		}
		base.Dispose(disposing);
	}
}
