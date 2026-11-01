using System;
using System.Drawing;
using System.Windows.Forms;

namespace EdrLocal;

public class OperationTimePanel : Panel
{
	private OperationTimePicker time;

	public DateTime Value
	{
		get
		{
			return time.Value;
		}
		set
		{
			time.Value = value;
		}
	}

	public OperationTimePanel(string title, DateTime value)
	{
		BackColor = Color.FromArgb(214, 214, 214);
		BorderStyle = BorderStyle.FixedSingle;
		Label value2 = new Label
		{
			Text = title,
			Bounds = new Rectangle(25, 11, 180, 39),
			TextAlign = ContentAlignment.MiddleRight,
			Font = new Font("Segoe UI", 14f, FontStyle.Bold),
			ForeColor = Color.Navy
		};
		Controls.Add(value2);
		time = new OperationTimePicker(value)
		{
			Bounds = new Rectangle(220, 9, 400, 38)
		};
		Controls.Add(time);
		GroupBox groupBox = new GroupBox
		{
			Text = "Dodaj do czasu " + ((title == "Przyjazd:") ? "przyjazdu" : "odjazdu"),
			Bounds = new Rectangle(10, 65, 640, 89),
			Font = ReferenceTheme.Dialog
		};
		Controls.Add(groupBox);
		int[] array = new int[16]
		{
			5, 10, 15, 20, 25, 30, 35, 40, 45, 50,
			60, 90, 120, 150, 180, 200
		};
		for (int i = 0; i < array.Length; i++)
		{
			int addition = array[i];
			SilverButton silverButton = new SilverButton
			{
				Text = addition + " min.",
				Bounds = new Rectangle(8 + i % 8 * 78, 20 + i / 8 * 30, 72, 27),
				Font = ReferenceTheme.Dialog
			};
			silverButton.Click += (object s, EventArgs e) =>
			{
				Value = Value.AddMinutes(addition);
			};
			groupBox.Controls.Add(silverButton);
		}
		GroupBox groupBox2 = new GroupBox
		{
			Text = "Dodaj do czasu - teksty",
			Bounds = new Rectangle(35, 170, (title == "Przyjazd:") ? 475 : 590, 82),
			Font = ReferenceTheme.Dialog
		};
		Controls.Add(groupBox2);
		string[] array2 = new string[4] { "Potrącenie [120 min.]", "Wypadek na przejeździe [120 min.]", "Uszkodzenie Sieci [70 min.]", "Defekt lokomotywy [70 min.]" };
		for (int num = 0; num < 4; num++)
		{
			int addition2 = ((num < 2) ? 120 : 70);
			SilverButton silverButton2 = new SilverButton
			{
				Text = array2[num],
				Bounds = new Rectangle(8 + num % 2 * (groupBox2.Width / 2 - 5), 20 + num / 2 * 28, groupBox2.Width / 2 - 12, 26),
				Font = ReferenceTheme.Dialog
			};
			silverButton2.Click += (object s, EventArgs e) =>
			{
				Value = Value.AddMinutes(addition2);
			};
			groupBox2.Controls.Add(silverButton2);
		}
	}
}
