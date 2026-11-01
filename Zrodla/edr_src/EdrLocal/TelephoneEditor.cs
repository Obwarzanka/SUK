using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EdrLocal;

public class TelephoneEditor : Editor
{
	private readonly Engine engine;

	private readonly string section;

	private readonly JournalEntry original;

	private readonly bool own;

	private readonly Panel body;

	private readonly FlowLayoutPanel sentence;

	private readonly TelephoneData initial;

	private readonly Label contentLabel;

	private readonly Label noteLabel;

	private readonly CheckBox noticeEnabled;

	private readonly ComboBox track;

	private readonly ComboBox mode;

	private readonly ComboBox cause;

	private readonly ComboBox arrival;

	private readonly CompactDatePicker date;

	private readonly TextBox time;

	private bool incomingBefore;

	public JournalEntry SavedEntry { get; private set; }

	public ComboBox TemplateBox { get; private set; }

	public TextBox SenderBox { get; private set; }

	public TextBox ReceiverBox { get; private set; }

	public TextBox EventTimeBox { get; private set; }

	public TextBox ReasonBox { get; private set; }

	public TextBox TrainBox { get; private set; }

	public TextBox WorkNameBox { get; private set; }

	public TextBox WorkRoleBox { get; private set; }

	public TextBox SpeedBox { get; private set; }

	public TextBox PassingTrainBox { get; private set; }

	public TextBox FreeBox { get; private set; }

	public TextBox NoteBox { get; private set; }

	public TextBox NoticeBox { get; private set; }

	public RadioButton Receive { get; private set; }

	public TelephoneEditor(Engine engine, string section, JournalEntry original)
		: base((section == "TEL") ? "Wpis do Dziennika Telefonicznego" : "Telefonogram do Dziennika Ruchu", 700, 600)
	{
		TelephoneEditor telephoneEditor = this;
		this.engine = engine;
		this.section = section;
		this.original = original;
		own = section == "TEL";
		body = AbsoluteBody();
		initial = ((original != null && original.Telephone != null) ? Json.Clone(original.Telephone) : new TelephoneData
		{
			TemplateId = (own ? "OWN" : "21"),
			SentAt = ((original == null) ? engine.Now : original.Time),
			EffectiveAt = ((original == null) ? engine.Now : original.Time),
			Track = 1,
			Reason = "",
			ClosureMode = "",
			FreeText = ((original == null) ? "" : original.Text),
			Incoming = (original != null && original.Direction == "Odbieram"),
			LocalParty = engine.User.Name,
			Sender = ((original != null && original.Direction == "Odbieram") ? original.Recipient : engine.User.Name),
			Receiver = ((original != null && original.Direction != "Odbieram") ? original.Recipient : "")
		});
		LabelAt(own ? engine.Station.Name : engine.Station.Sections.First((Section x) => x.Id == section).Name, 14, 6, 315, 26, bold: true);
		RadioButton control = new RadioButton
		{
			Text = "Nadaję",
			Checked = !initial.Incoming,
			Font = ReferenceTheme.Dialog
		};
		Put(control, 350, 10, 90, 22);
		Receive = new RadioButton
		{
			Text = "Odbieram",
			Checked = initial.Incoming,
			Font = ReferenceTheme.Dialog
		};
		Put(Receive, 450, 10, 110, 22);
		incomingBefore = initial.Incoming;
		LabelAt("Data nadania:", 14, 38, 315, 20, bold: false);
		LabelAt("Godzina nadania:", 350, 38, 315, 20, bold: false);
		date = new CompactDatePicker
		{
			Value = initial.SentAt,
			CustomFormat = "yyyy-MM-dd",
			Font = ReferenceTheme.Dialog
		};
		Put(date, 14, 59, 315, 24);
		time = ClockBox(initial.SentAt.ToString("HH:mm"));
		((ClockTextBox)time).TimeSeeded += (object s, EventArgs e) =>
		{
			date.Value = ((ClockTextBox)time).LastSeededTime;
		};
		Put(time, 350, 59, 315, 24);
		LabelAt("Nadał:", 14, 88, 315, 20, bold: false);
		LabelAt("Odebrał:", 350, 88, 315, 20, bold: false);
		SenderBox = Box(initial.Sender);
		ReceiverBox = Box(initial.Receiver);
		Put(SenderBox, 14, 109, 315, 24);
		Put(ReceiverBox, 350, 109, 315, 24);
		SenderBox.ReadOnly = !initial.Incoming;
		ReceiverBox.ReadOnly = initial.Incoming;
		TemplateBox = Ui.Combo(TelephoneTemplates.Titles, null, editable: false);
		TemplateBox.Font = ReferenceTheme.Dialog;
		TemplateBox.MaxDropDownItems = 8;
		TemplateBox.IntegralHeight = false;
		TemplateBox.DropDown += (object s, EventArgs e) =>
		{
			TemplateBox.ItemHeight = Math.Max(18, TemplateBox.Font.Height + 5);
			TemplateBox.DropDownHeight = TemplateBox.ItemHeight * 8 + 2;
			TemplateBox.DropDownWidth = Math.Max(TemplateBox.Width, Math.Min(Screen.FromControl(TemplateBox).WorkingArea.Width - 20, TelephoneTemplates.Titles.Max((string t) => TextRenderer.MeasureText(t, TemplateBox.Font).Width) + SystemInformation.VerticalScrollBarWidth + 18));
		};
		TemplateBox.SelectedIndex = Math.Max(0, Array.IndexOf(TelephoneTemplates.Ids, initial.TemplateId));
		TemplateBox.Enabled = original == null;
		noticeEnabled = new CheckBox
		{
			Text = "Zawiadomienie / Powiadomienie",
			AutoSize = true,
			Checked = !string.IsNullOrWhiteSpace(initial.Notice),
			Font = ReferenceTheme.Dialog
		};
		Put(noticeEnabled, 14, 202, 400, 22);
		NoticeBox = Box(initial.Notice);
		NoticeBox.Multiline = true;
		NoticeBox.ScrollBars = ScrollBars.Vertical;
		Put(NoticeBox, 14, 227, 651, 62);
		contentLabel = LabelAt("Adres i treść rozmowy lub telefonogramu:", 14, 230, 651, 20, bold: false);
		sentence = new FlowLayoutPanel
		{
			BackColor = Ui.Cream,
			WrapContents = true,
			AutoScroll = true,
			Padding = new Padding(7, 10, 5, 5),
			Margin = Padding.Empty
		};
		Put(sentence, 14, 252, 651, 125);
		track = Ui.Combo(from x in Enumerable.Range(1, own ? 1 : engine.Station.Sections.First((Section x) => x.Id == section).Tracks)
			select x.ToString(), initial.Track.ToString(), editable: false);
		track.Font = ReferenceTheme.Dialog;
		track.Width = 46;
		mode = Ui.Combo(new string[3] { "", "planowo", "awaryjnie" }, initial.ClosureMode ?? "", editable: false);
		mode.Font = ReferenceTheme.Dialog;
		mode.Width = 90;
		cause = Ui.Combo(new string[2] { "z powodu", "celem" }, initial.ForPurpose ? "celem" : "z powodu", editable: false);
		cause.Font = ReferenceTheme.Dialog;
		cause.Width = 92;
		arrival = Ui.Combo(new string[2] { "przyjechał", "przejechał" }, initial.Passed ? "przejechał" : "przyjechał", editable: false);
		arrival.Font = ReferenceTheme.Dialog;
		arrival.Width = 105;
		EventTimeBox = ClockBox((initial.LastTrainDeparture ?? initial.EffectiveAt).ToString("HH:mm"));
		EventTimeBox.Width = 72;
		ReasonBox = Box(initial.Reason);
		ReasonBox.Width = 235;
		TrainBox = Box(initial.TrainNumber);
		TrainBox.Width = 85;
		PassingTrainBox = Box(initial.PassingTrainNumber);
		PassingTrainBox.Width = 85;
		WorkNameBox = Box(initial.WorkSupervisorName);
		WorkNameBox.Width = 180;
		WorkRoleBox = Box(initial.WorkSupervisorRole);
		WorkRoleBox.Width = 180;
		SpeedBox = Box(initial.SpeedRestrictions);
		SpeedBox.Width = 270;
		noteLabel = LabelAt("Dodatkowa notatka:", 14, 389, 651, 20, bold: false);
		NoteBox = Box(initial.AdditionalNote);
		NoteBox.Multiline = true;
		NoteBox.ScrollBars = ScrollBars.Vertical;
		Put(NoteBox, 14, 411, 651, 102);
		FreeBox = Box(initial.FreeText);
		FreeBox.Multiline = true;
		FreeBox.ScrollBars = ScrollBars.Vertical;
		Put(FreeBox, 14, 177, 651, 270);
		FreeBox.Visible = own;
		if (own)
		{
			ClientSize = new Size(ClientSize.Width, 535);
			noticeEnabled.Visible = (NoticeBox.Visible = (sentence.Visible = (noteLabel.Visible = (NoteBox.Visible = false))));
			contentLabel.Top = 154;
		}
		else
		{
			LabelAt("Wzór telefonogramu:", 14, 147, 651, 20, bold: false);
			Put(TemplateBox, 14, 168, 651, 25);
			BuildSentence();
			ReflowNotice();
		}
		Receive.CheckedChanged += (object s, EventArgs e) =>
		{
			string text = (incomingBefore ? SenderBox.Text : ReceiverBox.Text);
			incomingBefore = Receive.Checked;
			SenderBox.Text = (incomingBefore ? text : initial.LocalParty);
			ReceiverBox.Text = (incomingBefore ? initial.LocalParty : text);
			SenderBox.ReadOnly = !incomingBefore;
			ReceiverBox.ReadOnly = incomingBefore;
			if (!own)
			{
				BuildSentence();
			}
		};
		TemplateBox.SelectedIndexChanged += (object s, EventArgs e) =>
		{
			BuildSentence();
			ReflowNotice();
		};
		noticeEnabled.CheckedChanged += (object s, EventArgs e) =>
		{
			ReflowNotice();
		};
		Save(() =>
		{
			telephoneEditor.SavedEntry = engine.SaveTelephone(section, original, telephoneEditor.ReadData());
		});
		foreach (Button control2 in Buttons.Controls)
		{
			control2.AutoSize = false;
			control2.MinimumSize = Size.Empty;
			control2.Size = new Size(100, 34);
			control2.Margin = new Padding(3, 1, 3, 1);
			control2.Font = new Font("Segoe UI", 10f);
		}
		Buttons.Height = 43;
	}

	private DateTime? ReadEventTime(DateTime day)
	{
		if (!(EventTimeBox is ClockTextBox { ChosenTime: not null } clockTextBox))
		{
			return Engine.JournalTime(((ClockTextBox)EventTimeBox).ValueText, day, null, null);
		}
		return clockTextBox.ChosenTime;
	}

	private TelephoneData ReadData()
	{
		DateTime? dateTime = Engine.JournalTime(((ClockTextBox)time).ValueText, date.Value.Date, null, null);
		if (!dateTime.HasValue)
		{
			throw new Exception("Podaj godzinę nadania.");
		}
		DateTime value = dateTime.Value;
		string text = (own ? "OWN" : TelephoneTemplates.Ids[TemplateBox.SelectedIndex]);
		bool flag = text == "23" || text == "26";
		if (flag)
		{
			goto IL_00ce;
		}
		switch (text)
		{
		case "24":
		case "11":
		case "12":
			goto IL_00ce;
		}
		int num = ((text == "21a") ? 1 : 0);
		goto IL_00cf;
		IL_00ce:
		num = 1;
		goto IL_00cf;
		IL_00cf:
		bool flag2 = (byte)num != 0;
		if (!own && (!flag2 || flag) && ((ClockTextBox)EventTimeBox).ValueText.Length == 0)
		{
			throw new Exception("Podaj godzinę we wzorze telefonogramu.");
		}
		DateTime? dateTime2 = (flag ? ReadEventTime(value.Date) : ((DateTime?)null));
		if (dateTime2.HasValue && dateTime2 > value)
		{
			dateTime2 = dateTime2.Value.AddDays(-1.0);
		}
		TelephoneData telephoneData = new TelephoneData();
		telephoneData.TemplateId = text;
		telephoneData.Incoming = Receive.Checked;
		telephoneData.SentAt = value;
		telephoneData.EffectiveAt = ((own || flag2) ? value : (ReadEventTime(value.Date) ?? value));
		telephoneData.LastTrainDeparture = dateTime2;
		telephoneData.Track = int.Parse(track.Text);
		telephoneData.Sender = SenderBox.Text;
		telephoneData.Receiver = ReceiverBox.Text;
		telephoneData.Reason = ReasonBox.Text;
		telephoneData.ClosureMode = mode.Text;
		telephoneData.ForPurpose = cause.SelectedIndex == 1;
		telephoneData.Passed = arrival.SelectedIndex == 1;
		telephoneData.TrainNumber = TrainBox.Text;
		telephoneData.PassingTrainNumber = PassingTrainBox.Text;
		telephoneData.WorkSupervisorName = WorkNameBox.Text;
		telephoneData.WorkSupervisorRole = WorkRoleBox.Text;
		telephoneData.SpeedRestrictions = SpeedBox.Text;
		telephoneData.Notice = ((!own && noticeEnabled.Checked) ? NoticeBox.Text : "");
		telephoneData.AdditionalNote = (own ? "" : NoteBox.Text);
		telephoneData.FreeText = FreeBox.Text;
		return telephoneData;
	}

	private void BuildSentence()
	{
		if (own)
		{
			return;
		}
		sentence.SuspendLayout();
		sentence.Controls.Clear();
		string text = TelephoneTemplates.Ids[TemplateBox.SelectedIndex];
		string text2 = initial.LocalStation ?? engine.Station.Name.ToUpper();
		string text3 = initial.NeighbourStation ?? engine.Station.Sections.First((Section x) => x.Id == section).Name.ToUpper();
		string from = (Receive.Checked ? text3 : text2);
		string to = (Receive.Checked ? text2 : text3);
		Action action = () =>
		{
			Word("od");
			Station(from);
			Word("do");
			Station(to);
		};
		if (text == "9")
		{
			Word("Nie wyprawiać pociągów od");
			Field(EventTimeBox);
			Word("aż do odwołania.");
		}
		if (text == "10")
		{
			Word("Wstrzymanie wyprawienia pociągów odwołuję o");
			Field(EventTimeBox);
			Word(".");
		}
		if (text == "11")
		{
			Word("Nie wyprawiać pociągu");
			Field(TrainBox);
			Word("do czasu przejazdu pociągu");
			Field(PassingTrainBox);
			Word(".");
		}
		if (text == "12")
		{
			Word("Nie wyprawiać żadnego pociągu do czasu przejazdu pociągu");
			Field(PassingTrainBox);
			Word(".");
		}
		switch (text)
		{
		case "16":
		case "16a":
		case "17":
		case "17a":
			Word("O");
			Field(EventTimeBox);
			Word(text switch
			{
				"17a" => "odwołuję radiotelefoniczne zapowiadanie pociągów po torze numer", 
				"16a" => "wprowadzam radiotelefoniczne zapowiadanie pociągów po torze numer", 
				"16" => "wprowadzam telefoniczne zapowiadanie pociągów po torze numer", 
				_ => "odwołuję telefoniczne zapowiadanie pociągów po torze numer", 
			});
			Field(track);
			action();
			if (text == "16" || text == "16a")
			{
				Word("z powodu");
				Field(ReasonBox);
			}
			Word(".");
			break;
		}
		if (text == "21" || text == "22")
		{
			Word("Tor numer");
			Field(track);
			action();
			Word((text == "21") ? "zamknięty" : "otwarty");
			if (text == "21")
			{
				Field(mode);
			}
			Word((text == "21") ? "od" : "o");
			Field(EventTimeBox);
			if (text == "21")
			{
				Field(cause);
				Field(ReasonBox);
			}
			Word(".");
		}
		if (text == "21a")
		{
			Word("Tor numer");
			Field(track);
			action();
			Word("zamknięty");
			Field(mode);
			Word("po przejeździe pociągu numer");
			Field(TrainBox);
			Field(cause);
			Field(ReasonBox);
			Word(".");
		}
		if (text == "022")
		{
			Word("Roboty na torze");
			Field(track);
			Word("szlaku");
			Station(from);
			Word("–");
			Station(to);
			Word("zostały ukończone o godzinie");
			Field(EventTimeBox);
			Word("Tor ten jest zdatny i nie ma przeszkód do ruchu pociągów.");
			Word("Ograniczenia prędkości:");
			Field(SpeedBox);
			Word("Zawiadamiający:");
			Field(WorkNameBox);
			Word("Stanowisko:");
			Field(WorkRoleBox);
		}
		if (text == "23" || text == "26")
		{
			if (text == "23")
			{
				Word("Od");
				Station(from);
				Word("do");
				Station(to);
				Word("wprowadzam ruch jednotorowy dwukierunkowy po torze numer");
			}
			else
			{
				Word("Przy czynnych obu torach wprowadzam");
				action();
				Word("ruch dwukierunkowy po torze numer");
			}
			Field(track);
			Word(", po którym ostatni pociąg");
			Field(TrainBox);
			Word("odjechał o");
			Field(EventTimeBox);
			Word(".");
		}
		if (text == "24")
		{
			Word("Wprowadzić ruch jednotorowy dwukierunkowy");
			action();
			Word("po torze numer");
			Field(track);
			Word(".");
		}
		if (text == "25")
		{
			Word("Przywracam ruch dwutorowy");
			action();
			Word("o");
			Field(EventTimeBox);
			Word(".");
		}
		if (text == "27")
		{
			Word("Przywracam ruch jednokierunkowy po torze numer");
			Field(track);
			action();
			Word("o");
			Field(EventTimeBox);
			Word(".");
		}
		if (text == "31" || text == "32")
		{
			Word((text == "31") ? "Tor numer" : "Po torze numer");
			Field(track);
			action();
			Word("od godziny");
			Field(EventTimeBox);
			Word((text == "31") ? "nieprzejezdny dla pociągów z trakcją elektryczną." : "przywracam ruch trakcją elektryczną.");
		}
		if (text == "28" || text == "29")
		{
			Word("Posterunek");
			Station(from);
			Word((text == "28") ? "otwarty o godzinie" : "zamknięty o godzinie");
			Field(EventTimeBox);
			Word(".");
		}
		if (text == "30")
		{
			Word("Zatrzymać pociągi, pociąg");
			Field(TrainBox);
			Field(arrival);
			Word("bez sygnału końca pociągu.");
		}
		sentence.ResumeLayout(performLayout: true);
	}

	private void ReflowNotice()
	{
		if (!own)
		{
			int num = (noticeEnabled.Checked ? 76 : 0);
			int num2 = ((TelephoneTemplates.Ids[TemplateBox.SelectedIndex] == "022") ? 75 : 0);
			NoticeBox.Visible = noticeEnabled.Checked;
			contentLabel.Top = 230 + num;
			sentence.Top = 252 + num;
			sentence.Height = 125 + num2;
			noteLabel.Top = 389 + num + num2;
			NoteBox.Top = 411 + num + num2;
			body.AutoScroll = true;
			ClientSize = new Size(ClientSize.Width, Math.Min(575 + num + num2, Screen.FromControl(this).WorkingArea.Height - 45));
		}
	}

	private void Word(string text)
	{
		sentence.Controls.Add(new Label
		{
			Text = text,
			AutoSize = true,
			Font = ReferenceTheme.Dialog,
			Margin = new Padding(1, 5, 1, 1),
			MaximumSize = new Size(615, 0)
		});
	}

	private void Station(string name)
	{
		sentence.Controls.Add(new Label
		{
			Text = name,
			AutoSize = true,
			BorderStyle = BorderStyle.FixedSingle,
			BackColor = Color.White,
			Font = ReferenceTheme.Dialog,
			Margin = new Padding(2, 1, 2, 1),
			Padding = new Padding(3),
			Tag = "FixedStation"
		});
	}

	private void Field(Control field)
	{
		field.Dock = DockStyle.None;
		field.Height = 25;
		field.Margin = new Padding(2, 1, 2, 1);
		sentence.Controls.Add(field);
	}

	private TextBox ClockBox(string text)
	{
		ClockTextBox box = new ClockTextBox
		{
			Text = (text ?? ""),
			Font = ReferenceTheme.Dialog,
			BackColor = Color.White,
			Clock = () => date.Value.Date.Add(engine.Now.TimeOfDay)
		};
		box.ValueProvider = () => Engine.JournalTime(box.Text, date.Value.Date, null, null);
		return box;
	}

	private TextBox Box(string text)
	{
		TextBox textBox = new TextBox();
		textBox.Text = text ?? "";
		textBox.Font = ReferenceTheme.Dialog;
		textBox.BackColor = Color.White;
		return textBox;
	}

	private void Put(Control control, int x, int y, int width, int height)
	{
		control.Dock = DockStyle.None;
		control.Bounds = new Rectangle(x, y, width, height);
		body.Controls.Add(control);
	}

	private Label LabelAt(string text, int x, int y, int width, int height, bool bold)
	{
		Label label = new Label();
		label.Text = text;
		label.Font = (bold ? new Font("Segoe UI", 9f, FontStyle.Bold) : ReferenceTheme.Dialog);
		label.Bounds = new Rectangle(x, y, width, height);
		label.TextAlign = ContentAlignment.MiddleLeft;
		Label label2 = label;
		body.Controls.Add(label2);
		return label2;
	}
}
