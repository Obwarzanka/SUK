using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EdrLocal;

public sealed class CrossingNotificationForm : Editor
{
	private sealed class Item
	{
		public CrossingSetting Crossing;

		public CheckBox Mark;

		public ClockTextBox Time;

		public DateTime Day;
	}

	private readonly List<Item> items = new List<Item>();

	private readonly Engine engine;

	private readonly JournalEntry original;

	private readonly Section section;

	public CrossingNotificationForm(Engine owner, JournalEntry entry, Section configuration)
		: base("Powiadomienia dróżników przejazdowych", 620, Math.Min(730, 220 + CrossingRules.All(configuration).Count * 43))
	{
		CrossingNotificationForm crossingNotificationForm = this;
		engine = owner;
		original = entry;
		section = configuration;
		Fields.ColumnStyles[0].Width = 260f;
		Info("Szlak do posterunku: " + section.Name + "     Nr pociągu: " + entry.Number, 35);
		Button button = Ui.Button("Powiadom wszystkich", NotifyAll);
		button.Name = "notifyAll";
		Add("", button, 39);
		List<CrossingSetting> source = CrossingRules.All(section);
		Func<CrossingSetting, bool> predicate = (CrossingSetting x) => CrossingRules.Required(entry, crossingNotificationForm.section, x);
		foreach (CrossingSetting crossing in source.Where(predicate))
		{
			CrossingNotification crossingNotification = CrossingRules.Find(entry, section, crossing);
			DateTime? time = crossingNotification.RecordedAt ?? CrossingRules.Time(crossingNotification.Value, entry.Time);
			Panel panel = new Panel();
			CheckBox mark = new CheckBox
			{
				Text = "Powiadomiono",
				Checked = !string.IsNullOrEmpty(crossingNotification.Value),
				Bounds = new Rectangle(0, 0, 125, 26)
			};
			panel.Controls.Add(mark);
			Item item = new Item
			{
				Crossing = crossing,
				Mark = mark,
				Day = (time ?? entry.Time).Date
			};
			if (CrossingRules.UsesTime(section, crossing) || crossing.RecordClosure == true)
			{
				ClockTextBox clock = new ClockTextBox
				{
					Text = Ui.Time(time),
					Clock = () => engine.Now,
					Bounds = new Rectangle(130, 0, 70, 26)
				};
				panel.Controls.Add(clock);
				item.Time = clock;
				clock.ValueProvider = () => ClockDates.Resolve(clock.Text, item.Day, time, null, crossingNotificationForm.engine.Now);
				clock.TimeSeeded += (object s, EventArgs e) =>
				{
					item.Day = clock.LastSeededTime.Date;
					mark.Checked = true;
				};
				clock.TextChanged += (object s, EventArgs e) =>
				{
					if (!clock.IsChoosing && !clock.IsSuggestion)
					{
						mark.Checked = ClockMask.Value(clock.Text).Length > 0;
					}
				};
				mark.CheckedChanged += (object s, EventArgs e) =>
				{
					if (mark.Checked && CrossingRules.UsesTime(crossingNotificationForm.section, crossing))
					{
						clock.FillCurrentTime();
					}
				};
			}
			items.Add(item);
			Add(CrossingRules.Label(crossing) + "  " + crossing.Name, panel, 43);
		}
		if (items.Count == 0)
		{
			Info("Brak dróżników wymagających powiadomienia dla tego pociągu.", 45);
		}
		Save(SaveNotifications);
		((Button)AcceptButton).Text = "OK";
	}

	internal void NotifyAll()
	{
		foreach (Item item in items)
		{
			item.Mark.Checked = true;
			if (item.Time != null)
			{
				item.Time.FillCurrentTime();
			}
		}
	}

	internal void SaveNotifications()
	{
		engine.RequireWrite();
		if (!engine.CanEditJournal(original.SectionId))
		{
			throw new Exception("Dziennik jest dostępny tylko do odczytu.");
		}
		JournalEntry journalEntry = engine.State.Entries.FirstOrDefault((JournalEntry x) => x.Id == original.Id);
		if (journalEntry == null || journalEntry.Cancelled)
		{
			throw new Exception("Wpis pociągu nie jest już aktywny.");
		}
		JournalEntry entry = Json.Clone(journalEntry);
		foreach (Item item in items)
		{
			if (CrossingRules.Required(entry, section, item.Crossing))
			{
				DateTime? dateTime = null;
				if (item.Mark.Checked && item.Time != null && item.Time.ValueText.Length > 0)
				{
					dateTime = Engine.JournalTime(item.Time.ValueText, item.Day, null, null);
				}
				if (item.Mark.Checked && CrossingRules.UsesTime(section, item.Crossing) && !dateTime.HasValue)
				{
					throw new Exception("Wpisz godzinę powiadomienia dróżnika " + CrossingRules.Label(item.Crossing) + ".");
				}
				string value;
				if (item.Mark.Checked)
				{
					value = (CrossingRules.UsesTime(section, item.Crossing) ? Ui.Time(dateTime) : "/");
				}
				else
				{
					value = "";
				}
				CrossingRules.Set(entry, section, item.Crossing, value, dateTime);
			}
		}
		engine.SaveJournal(entry);
	}
}
