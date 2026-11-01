using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;

namespace EdrLocal;

public sealed class JournalReport : IDisposable
{
	public const float PageWidth = 827f;

	public const float PageHeight = 1169f;

	public const float TableLeft = 48f;

	public const float TableTop = 151f;

	public const float TableWidth = 730f;

	public const float BodyBottom = 1071f;

	private float signatureHeight;

	private Section reportSection;

	private List<CrossingSetting> reportCrossings = new List<CrossingSetting>();

	public bool Telephone;

	public bool WithHistory;

	public string StationName;

	public string SectionName;

	public string Author;

	public DateTime From;

	public DateTime To;

	public DateTime Generated = DateTime.Now;

	public List<ReportRow> Rows = new List<ReportRow>();

	public float[] Widths;

	private readonly List<List<ReportDrawRow>> pages = new List<List<ReportDrawRow>>();

	private readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();

	public string OrganizationName { get; private set; }

	private int NotificationSpan
	{
		get
		{
			if (reportCrossings.Count != 0)
			{
				return reportCrossings.Count;
			}
			return 2;
		}
	}

	private string SignatureText => "Wyciąg sporządził: " + Author + "\nJednostka: " + OrganizationName + "\ndnia " + Generated.ToString("yyyy-MM-dd") + " godz. " + Generated.ToString("HH:mm");

	public int PageCount => pages.Count;

	private float HeaderHeight => Telephone ? 100 : 82;

	private Font Font(float size, FontStyle style)
	{
		string key = size + "/" + style;
		if (!fonts.ContainsKey(key))
		{
			fonts[key] = new Font("Times New Roman", size, style, GraphicsUnit.Pixel);
		}
		return fonts[key];
	}

	public JournalReport(Engine engine, List<JournalEntry> entries, bool telephone, bool history, Section section, DateTime from, DateTime to)
	{
		Telephone = telephone;
		WithHistory = history;
		StationName = engine.Station.Name.ToUpper();
		SectionName = ((section == null) ? "" : (engine.IsTd2 ? StationSettings.Title(section) : section.Name));
		Author = engine.User.Name;
		OrganizationName = engine.OrganizationCaption;
		From = from;
		To = to;
		Widths = ((!telephone) ? new float[12]
		{
			48f, 48f, 32f, 22f, 22f, 22f, 22f, 22f, 22f, 330f,
			70f, 70f
		} : new float[6] { 57f, 75f, 400f, 41f, 41f, 116f });
		reportSection = section;
		if (!telephone && section != null)
		{
			reportCrossings = CrossingRules.Visible(section);
			if (reportCrossings.Count > 0)
			{
				Widths = Widths.Take(10).Concat(Enumerable.Repeat(140f / (float)reportCrossings.Count, reportCrossings.Count)).ToArray();
			}
		}
		if (history)
		{
			Widths[telephone ? 2 : 9] -= 70f;
			Widths = Widths.Concat(new float[1] { 70f }).ToArray();
		}
		List<JournalEntry> list = WorkTrip.Order(entries.Where((JournalEntry e) => (!telephone) ? (e.SectionId != "TEL" && (section == null || e.SectionId == section.Id)) : (e.SectionId == "TEL")));
		Dictionary<string, List<ReportVersion>> dictionary = (history ? ReportHistory.Read(engine.State, list) : null);
		Dictionary<string, int> dictionary2 = TelephoneNumbering.Numbers(engine.State, engine.Station);
		DateTime? dateTime = null;
		foreach (JournalEntry item2 in list)
		{
			if (dateTime != item2.Time.Date)
			{
				dateTime = item2.Time.Date;
				ReportRow reportRow = new ReportRow
				{
					Day = true,
					Date = dateTime.Value
				};
				reportRow.Cells.Add(new ReportCell(0, Widths.Length, "Północ dnia " + dateTime.Value.ToString("dd-MM-yyyy"), left: false));
				Rows.Add(reportRow);
			}
			foreach (ReportVersion item3 in history ? dictionary[item2.Id] : new List<ReportVersion>
			{
				new ReportVersion
				{
					Entry = item2
				}
			})
			{
				dictionary2.TryGetValue(item2.Id, out var value);
				Rows.Add(MakeRow(item3, value));
			}
		}
		if (list.Count == 0)
		{
			ReportRow item = new ReportRow
			{
				Cells = 
				{
					new ReportCell(0, Widths.Length, "Brak wpisów w wybranym okresie.", left: false)
				}
			};
			Rows.Add(item);
		}
		Paginate();
	}

	private ReportRow MakeRow(ReportVersion version, int number)
	{
		JournalEntry entry = version.Entry;
		ReportRow reportRow = new ReportRow();
		reportRow.Previous = version.Previous;
		reportRow.Bold = WithHistory && !version.Previous;
		reportRow.Strike = entry.Cancelled;
		reportRow.Date = entry.Time.Date;
		ReportRow reportRow2 = reportRow;
		int span = Widths.Length - (WithHistory ? 1 : 0);
		string text = entry.Text ?? "";
		if (!Telephone && entry.Type == "Telefonogram")
		{
			text = text + "\n" + TelephonePresentation.JournalLine(entry);
		}
		if (entry.Cancelled)
		{
			text = text + ((text.Length == 0) ? "" : "\n") + "Wykreślono: " + entry.CancelReason;
		}
		if (entry.Type == "Służbowy")
		{
			if (text.StartsWith("Dyżur przyjął") && !text.Contains("godzinie"))
			{
				text = text.TrimEnd('.') + " dnia " + entry.Time.ToString("dd-MM-yyyy") + " o godzinie " + entry.Time.ToString("HH:mm");
			}
			reportRow2.Cells.Add(new ReportCell(0, span, text, left: false));
		}
		else if (Telephone)
		{
			DateTime dateTime = ((entry.Telephone == null) ? entry.Time : entry.Telephone.SentAt);
			string text2 = ((entry.Telephone == null) ? entry.User : entry.Telephone.Sender);
			string text3 = ((entry.Telephone == null) ? entry.Recipient : entry.Telephone.Receiver);
			reportRow2.Cells.Add(new ReportCell(0, 1, number.ToString(), left: false));
			reportRow2.Cells.Add(new ReportCell(1, 1, dateTime.ToString("yyyy-MM-dd"), left: false));
			reportRow2.Cells.Add(new ReportCell(2, 1, text, left: true));
			reportRow2.Cells.Add(new ReportCell(3, 1, dateTime.ToString("HH"), left: false));
			reportRow2.Cells.Add(new ReportCell(4, 1, dateTime.ToString("mm"), left: false));
			reportRow2.Cells.Add(new ReportCell(5, 1, (text2 ?? "") + (string.IsNullOrWhiteSpace(text3) ? "" : (" /\n" + text3)), left: false));
		}
		else if (entry.Type == "Pociąg")
		{
			char[] array = (entry.Number ?? "").Where(char.IsDigit).ToArray();
			bool flag = array.Length > 0 && (array[array.Length - 1] - 48) % 2 == 0;
			reportRow2.Cells.Add(new ReportCell(0, 1, (flag || !string.IsNullOrEmpty(entry.ReturnOfId)) ? "" : entry.Number, left: false));
			reportRow2.Cells.Add(new ReportCell(1, 1, (flag && string.IsNullOrEmpty(entry.ReturnOfId)) ? entry.Number : "", left: false));
			reportRow2.Cells.Add(new ReportCell(2, 1, entry.Track, left: false));
			AddTime(reportRow2, 3, entry.Permission);
			AddTime(reportRow2, 5, entry.Left);
			AddTime(reportRow2, 7, entry.Arrived);
			reportRow2.Cells.Add(new ReportCell(9, 1, text, left: true));
			if (reportCrossings.Count == 0)
			{
				reportRow2.Cells.Add(new ReportCell(10, 2, entry.Notification, left: false));
			}
			else
			{
				for (int i = 0; i < reportCrossings.Count; i++)
				{
					reportRow2.Cells.Add(new ReportCell(10 + i, 1, CrossingRules.Value(entry, reportSection, reportCrossings[i]), left: false));
				}
			}
		}
		else
		{
			reportRow2.Cells.Add(new ReportCell(0, span, text, left: true));
		}
		if (WithHistory)
		{
			reportRow2.Cells.Add(new ReportCell(Widths.Length - 1, 1, version.Changed.HasValue ? version.Changed.Value.ToString("dd-MM-yyyy\nHH:mm:ss") : "", left: false));
		}
		return reportRow2;
	}

	private static void AddTime(ReportRow r, int n, DateTime? t)
	{
		r.Cells.Add(new ReportCell(n, 1, t.HasValue ? t.Value.ToString("HH") : "-", left: false));
		r.Cells.Add(new ReportCell(n + 1, 1, t.HasValue ? t.Value.ToString("mm") : "-", left: false));
	}

	private float CellWidth(ReportCell c)
	{
		return Widths.Skip(c.Column).Take(c.Span).Sum();
	}

	private float CellLeft(ReportCell c)
	{
		return 48f + Widths.Take(c.Column).Sum();
	}

	private Font RowFont(ReportRow r)
	{
		return Font(12.3f, (FontStyle)((r.Bold ? 1 : 0) | 2 | (r.Strike ? 8 : 0)));
	}

	private Font CellFont(ReportRow row, ReportCell cell)
	{
		if (!WithHistory || cell.Column != Widths.Length - 1)
		{
			return RowFont(row);
		}
		return Font(8.5f, (!row.Previous) ? FontStyle.Bold : FontStyle.Regular);
	}

	private static string[] Wrap(Graphics g, string value, Font font, float width)
	{
		using StringFormat format = new StringFormat(StringFormat.GenericTypographic);
		List<string> list = new List<string>();
		string[] array = (value ?? "").Replace("\r", "").Split('\n');
		foreach (string text in array)
		{
			string text2 = "";
			string[] array2 = text.Split(' ');
			foreach (string text3 in array2)
			{
				if (g.MeasureString((text2.Length == 0) ? text3 : (text2 + " " + text3), font, int.MaxValue, format).Width <= width)
				{
					text2 = ((text2.Length == 0) ? text3 : (text2 + " " + text3));
					continue;
				}
				if (text2.Length > 0)
				{
					list.Add(text2);
					text2 = "";
				}
				string text4 = "";
				string text5 = text3;
				foreach (char c in text5)
				{
					if (text4.Length > 0 && g.MeasureString(text4 + c, font, int.MaxValue, format).Width > width)
					{
						list.Add(text4);
						text4 = "";
					}
					text4 += c;
				}
				text2 = text4;
			}
			list.Add(text2);
		}
		return list.ToArray();
	}

	private void Paginate()
	{
		using Bitmap image = new Bitmap(1, 1);
		Graphics g = Graphics.FromImage(image);
		try
		{
			List<ReportDrawRow> list = new List<ReportDrawRow>();
			pages.Add(list);
			float num = 151f + HeaderHeight;
			foreach (ReportRow row in Rows)
			{
				List<ReportCell> cells = row.Cells;
				Func<ReportCell, string[]> selector = (ReportCell c) => Wrap(g, c.Text, CellFont(row, c), CellWidth(c) - 7f);
				List<string[]> source = cells.Select(selector).ToList();
				int num2 = source.Max((string[] x) => x.Length);
				int take;
				for (int offset = 0; offset < num2; offset += take)
				{
					int num3 = (int)Math.Floor((1071f - num - 6f) / 15f);
					if (num3 < 1)
					{
						list = new List<ReportDrawRow>();
						pages.Add(list);
						num = 151f + HeaderHeight;
						num3 = (int)Math.Floor((1071f - num - 6f) / 15f);
					}
					take = Math.Min(num3, num2 - offset);
					float num4 = Math.Max(20, source.Select((string[] x, int n) => Math.Max(0, Math.Min(take, x.Length - offset)) * ((WithHistory && row.Cells[n].Column == Widths.Length - 1) ? 10 : 15) + 6).Max());
					list.Add(new ReportDrawRow
					{
						Row = row,
						Top = num,
						Height = num4,
						Lines = source.Select((string[] x) => x.Skip(offset).Take(take).ToArray()).ToList()
					});
					num += num4;
				}
			}
			signatureHeight = Math.Max(59, Wrap(g, SignatureText, Font(13f, FontStyle.Regular), 650f).Length * 16 + 5);
			if (num + 25f + signatureHeight > 1071f)
			{
				pages.Add(new List<ReportDrawRow>());
			}
		}
		finally
		{
			if (g != null)
			{
				((IDisposable)g).Dispose();
			}
		}
	}

	private void Text(Graphics g, string text, Font f, Brush ink, RectangleF area, bool left)
	{
		StringFormat stringFormat = new StringFormat(StringFormat.GenericTypographic);
		stringFormat.Alignment = ((!left) ? StringAlignment.Center : StringAlignment.Near);
		stringFormat.LineAlignment = StringAlignment.Center;
		stringFormat.Trimming = StringTrimming.None;
		stringFormat.FormatFlags = StringFormatFlags.LineLimit;
		using StringFormat format = stringFormat;
		g.DrawString(text, f, ink, area, format);
	}

	private void Box(Graphics g, int column, int span, float y, float height, string text, Font f)
	{
		float num = 48f + Widths.Take(column).Sum();
		float num2 = Widths.Skip(column).Take(span).Sum();
		g.DrawRectangle(Pens.Black, num, y, num2, height);
		Text(g, text, f, Brushes.Black, new RectangleF(num + 2f, y + 1f, num2 - 4f, height - 2f), left: false);
	}

	private void Header(Graphics g)
	{
		string text = "Wyciąg z dziennika " + (Telephone ? "telefonicznego" : "ruchu") + (WithHistory ? " z historią zmian." : ".");
		Text(g, text, Font(19f, FontStyle.Regular), Brushes.Black, new RectangleF(0f, 57f, 827f, 27f), left: false);
		Text(g, "Posterunek: " + StationName, Font(13f, FontStyle.Bold), Brushes.Black, new RectangleF(80f, 96f, 315f, 23f), left: true);
		Text(g, "za okres: " + From.ToString("yyyy-MM-dd HH:mm") + " - " + To.ToString("yyyy-MM-dd HH:mm"), Font(12f, FontStyle.Bold), Brushes.Black, new RectangleF(400f, 96f, 385f, 23f), left: true);
		if (!Telephone)
		{
			Text(g, "Szlak z i do: " + SectionName, Font(13f, FontStyle.Bold), Brushes.Black, new RectangleF(80f, 116f, 680f, 23f), left: true);
		}
		Font f = Font(11.3f, FontStyle.Regular);
		float num = 151f;
		if (Telephone)
		{
			Box(g, 0, 1, num, 100f, "Nr\nkol.", f);
			Box(g, 1, 1, num, 100f, "Data", f);
			Box(g, 2, 1, num, 100f, "Adres i treść\nrozmowy lub telefonogramu", f);
			Box(g, 3, 2, num, 80f, "Czas zakończenia rozmowy lub nadawania telefonogramu", f);
			Box(g, 3, 1, num + 80f, 20f, "godz.", f);
			Box(g, 4, 1, num + 80f, 20f, "min.", f);
			Box(g, 5, 1, num, 100f, "Nadał /\nOdebrał", f);
			if (WithHistory)
			{
				Box(g, 6, 1, num, 100f, "Data i\ngodzina\nzmiany", f);
			}
			return;
		}
		Box(g, 0, 2, num, 50f, "Nr pociągu", f);
		Box(g, 0, 1, num + 50f, 18f, "nieparzysty", Font(9.5f, FontStyle.Regular));
		Box(g, 1, 1, num + 50f, 18f, "parzysty", Font(9.5f, FontStyle.Regular));
		Box(g, 2, 1, num, 68f, "Tor\nsta-\ncyj-\nny", f);
		string[] array = new string[3] { "Droga\nwolna", "Pociąg\nodjechał", "Pociąg\nprzyje-\nchał" };
		for (int i = 0; i < 3; i++)
		{
			int num2 = 3 + i * 2;
			Box(g, num2, 2, num, 50f, array[i], Font(10.5f, FontStyle.Regular));
			Box(g, num2, 1, num + 50f, 18f, "g.", f);
			Box(g, num2 + 1, 1, num + 50f, 18f, "m.", f);
		}
		Box(g, 9, 1, num, 68f, "Uwagi", f);
		Box(g, 10, NotificationSpan, num, 50f, "O jeździe pociągu zawiadomiono dróżników", f);
		for (int j = 0; j < NotificationSpan; j++)
		{
			Box(g, 10 + j, 1, num + 50f, 18f, (reportCrossings.Count == 0) ? "" : CrossingRules.Label(reportCrossings[j]), f);
		}
		int[] array2 = new int[8] { 0, 1, 2, 3, 5, 7, 9, 10 };
		int[] array3 = new int[8] { 1, 1, 1, 2, 2, 2, 1, NotificationSpan };
		for (int k = 0; k < 8; k++)
		{
			Box(g, array2[k], array3[k], num + 68f, 14f, (k + 1).ToString(), Font(9.5f, FontStyle.Regular));
		}
		if (WithHistory)
		{
			Box(g, Widths.Length - 1, 1, num, 68f, "Data i\ngodzina\nzmiany", f);
			Box(g, Widths.Length - 1, 1, num + 68f, 14f, "9", f);
		}
	}

	public void DrawPage(Graphics g, int pageIndex)
	{
		g.FillRectangle(Brushes.White, 0f, 0f, 827f, 1169f);
		g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
		g.SmoothingMode = SmoothingMode.AntiAlias;
		Header(g);
		foreach (ReportDrawRow item in pages[pageIndex])
		{
			ReportRow row = item.Row;
			Brush ink = (row.Previous ? Brushes.Gray : Brushes.Black);
			RowFont(row);
			for (int i = 0; i < row.Cells.Count; i++)
			{
				ReportCell reportCell = row.Cells[i];
				float num = CellLeft(reportCell);
				float num2 = CellWidth(reportCell);
				g.DrawRectangle(Pens.Black, num, item.Top, num2, item.Height);
				Text(g, string.Join("\n", item.Lines[i]), CellFont(row, reportCell), ink, new RectangleF(num + 3f, item.Top + 3f, num2 - 6f, item.Height - 5f), reportCell.Left);
			}
		}
		if (pageIndex == pages.Count - 1)
		{
			float y = ((pages[pageIndex].Count == 0) ? (151f + HeaderHeight + 25f) : (pages[pageIndex].Last().Top + pages[pageIndex].Last().Height + 25f));
			Text(g, SignatureText, Font(13f, FontStyle.Regular), Brushes.Black, new RectangleF(80f, y, 650f, signatureHeight), left: true);
		}
		Text(g, pageIndex + 1 + " / " + pages.Count, Font(10f, FontStyle.Regular), Brushes.Black, new RectangleF(48f, 1120f, 730f, 18f), left: false);
	}

	public Bitmap RenderPage(int index, int scale)
	{
		Bitmap bitmap = new Bitmap(827 * scale, 1169 * scale);
		bitmap.SetResolution(100 * scale, 100 * scale);
		using Graphics graphics = Graphics.FromImage(bitmap);
		graphics.ScaleTransform(scale, scale);
		DrawPage(graphics, index);
		return bitmap;
	}

	public void Export(string path)
	{
		LocalPersistence.RequireEnabled();
		string text = Path.GetExtension(path).ToLowerInvariant();
		if (text == ".pdf")
		{
			RasterPdf.Write(path, this);
			return;
		}
		if (text == ".csv")
		{
			string[] array = (Telephone ? new string[6] { "Nr kol.", "Data", "Adres i treść rozmowy lub telefonogramu", "Czas - godz.", "Czas - min.", "Nadał / Odebrał" } : new string[12]
			{
				"Nr pociągu nieparzysty", "Nr pociągu parzysty", "Tor stacyjny", "Droga wolna - godz.", "Droga wolna - min.", "Pociąg odjechał - godz.", "Pociąg odjechał - min.", "Pociąg przyjechał - godz.", "Pociąg przyjechał - min.", "Uwagi",
				"O jeździe zawiadomiono dróżników", ""
			});
			if (!Telephone && reportCrossings.Count > 0)
			{
				array = array.Take(10).Concat(reportCrossings.Select((CrossingSetting x) => "Dróżnik " + CrossingRules.Label(x))).ToArray();
			}
			if (WithHistory)
			{
				array = array.Concat(new string[1] { "Data i godzina zmiany" }).ToArray();
			}
			List<string[]> list = new List<string[]>();
			list.Add(array);
			List<string[]> list2 = list;
			foreach (ReportRow row in Rows)
			{
				string[] array2 = new string[Widths.Length];
				foreach (ReportCell cell in row.Cells)
				{
					array2[cell.Column] = cell.Text;
				}
				list2.Add(array2);
			}
			Csv.Write(path, list2);
			return;
		}
		StringBuilder stringBuilder = new StringBuilder("<!doctype html><html lang='pl'><meta charset='utf-8'><title>Wyciąg z dziennika</title><style>body{background:#aaa;margin:0}.page{display:block;background:white;width:210mm;margin:5mm auto}@media print{@page{size:A4 portrait;margin:0}body{background:white}.page{margin:0;width:210mm;height:297mm;break-after:page}.page:last-child{break-after:auto}}</style>");
		for (int num = 0; num < PageCount; num++)
		{
			using Bitmap bitmap = RenderPage(num, 2);
			using MemoryStream memoryStream = new MemoryStream();
			bitmap.Save(memoryStream, ImageFormat.Png);
			stringBuilder.Append("<img class='page' alt='Strona " + (num + 1) + "' src='data:image/png;base64," + Convert.ToBase64String(memoryStream.ToArray()) + "'>");
		}
		stringBuilder.Append("</html>");
		File.WriteAllText(path, stringBuilder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
	}

	public PrintDocument Printable()
	{
		PrintDocument printDocument = new PrintDocument();
		printDocument.DocumentName = "Wyciąg z dziennika " + (Telephone ? "telefonicznego" : "ruchu");
		printDocument.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);
		printDocument.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
		int index = 0;
		printDocument.BeginPrint += (object s, PrintEventArgs e) =>
		{
			index = 0;
		};
		printDocument.PrintPage += (object s, PrintPageEventArgs e) =>
		{
			GraphicsState gstate = e.Graphics.Save();
			e.Graphics.TranslateTransform(0f - e.PageSettings.HardMarginX, 0f - e.PageSettings.HardMarginY);
			e.Graphics.ScaleTransform((float)e.PageBounds.Width / 827f, (float)e.PageBounds.Height / 1169f);
			DrawPage(e.Graphics, index++);
			e.Graphics.Restore(gstate);
			e.HasMorePages = index < PageCount;
		};
		return printDocument;
	}

	public void Dispose()
	{
		foreach (Font value in fonts.Values)
		{
			value.Dispose();
		}
	}
}
