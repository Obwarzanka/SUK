using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace EdrLocal;

public static class Update199Tests
{
	private struct R
	{
		public int L;

		public int T;

		public int Right;

		public int B;
	}

	private struct ComboInfo
	{
		public int Size;

		public R Item;

		public R Button;

		public int State;

		public IntPtr Combo;

		public IntPtr Edit;

		public IntPtr List;
	}

	private static readonly List<string> log = new List<string>();

	[DllImport("user32.dll")]
	private static extern bool GetComboBoxInfo(IntPtr h, ref ComboInfo info);

	[DllImport("user32.dll")]
	private static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);

	private static void Check(bool ok, string text)
	{
		if (!ok)
		{
			throw new Exception("FAIL: " + text);
		}
		log.Add("PASS: " + text);
	}

	private static void Reject(Action action, string text)
	{
		bool ok = false;
		try
		{
			action();
		}
		catch
		{
			ok = true;
		}
		Check(ok, text);
	}

	private static T Field<T>(object o, string n)
	{
		return (T)o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
	}

	private static void Pump()
	{
		for (int i = 0; i < 5; i++)
		{
			Application.DoEvents();
		}
	}

	private static Td2Record Record(string number, string category, string consist)
	{
		Td2Record td2Record = new Td2Record();
		td2Record.Schedule = new Td2Schedule
		{
			trainInfo = new Td2TrainInfo
			{
				trainNo = number,
				trainCategoryCode = category,
				dataCon = consist
			}
		};
		return td2Record;
	}

	private static Td2IdentityResult Identity(string category, string consist, string number = "999999999")
	{
		return Td2Identity.Resolve(Record(number, category, consist));
	}

	private static void IdentityChecks()
	{
		string[][] array = new string[15][]
		{
			new string[2] { "ET22-468", "CTL Logistics Sp. z o.o." },
			new string[2] { "ET22-5151", "Pol-Trans sp. z o.o." },
			new string[2] { "ET22-5281", "Alza Cargo" },
			new string[2] { "ET22-5331", "Pol-Trans sp. z o.o." },
			new string[2] { "ET22-999", "PKP Cargo S.A." },
			new string[2] { "ET41-001", "PKP Cargo S.A." },
			new string[2] { "ST44-999", "PKP Cargo S.A." },
			new string[2] { "EU07-088", "Ecco Rail Sp. z o.o." },
			new string[2] { "EU07-476", "Orion Kolej Sp. z o.o." },
			new string[2] { "EU07E-082", "Majkoltrans Sp. z o.o." },
			new string[2] { "M62-1242", "LOTOS Kolej Sp. z o.o." },
			new string[2] { "M62-1151", "Pomorskie Przedsiębiorstwo Mechaniczno-Torowe Sp. z o.o." },
			new string[2] { "CTLR4C-001", "Rail4Chem mbH, Essen" },
			new string[2] { "SM42-820", "PKP Energetyka Sp. z o.o." },
			new string[2] { "4E-069", "Dolnośląskie Linie Autobusowe Sp. z o.o." }
		};
		string[][] array2 = array;
		foreach (string[] array3 in array2)
		{
			Td2IdentityResult td2IdentityResult = Identity("TME", array3[0] + ":parametry;WAGON_NIEZNANY");
			Check(td2IdentityResult.Carrier == array3[1] && td2IdentityResult.CarrierSource.Contains(array3[0]), "Towarowy " + array3[0] + " wskazuje " + array3[1] + " na podstawie pojazdu.");
		}
		Check(Identity("TME", "et22-468v2").Carrier == "CTL Logistics Sp. z o.o.", "Wariant i wielkość liter nie zamieniają prywatnego ET22 w domyślne Cargo.");
		Check(Identity("TME", "ET220-468").Carrier == "" && Identity("TME", "EU07-9999").Carrier == "", "Nieznany model i numer bez reguły nie dostają przypadkowego przewoźnika.");
		Check(Identity("TME", "ET22-468", "1000").Name == "" && Identity("TME", "ET22-468", "1000").Carrier.StartsWith("CTL"), "Numer towarowego nie uruchamia katalogu nazw pasażerskich.");
		Check(Identity("ROJ", "2EN57-038+1715").Carrier == "Koleje Mazowieckie Sp. z o.o.", "Dwukrotny EZT pozostaje jednym rozpoznanym modelem.");
		Check(Identity("ROJ", "SN81-001").Carrier == "POLREGIO S.A." && Identity("ROJ", "EN71-044").Carrier.Contains("Trójmieście"), "Przewoźnicy SZT i EZT pochodzą z przypisań pojazdów użytkownika.");
		Check(Identity("MPE", "EP07-1009;EN57-038").Operator == "KM", "Silna reguła zespołu trakcyjnego ma przewagę nad samą lokomotywą.");
		Check(Identity("MPE", "ET22-243;110a_PKP_Bcwxz_01;110a_PKP_Bcwxz_01").Operator == "PKPC", "Turystyczny skład z lokomotywą Cargo zachowuje rozstrzygnięcie SAZ.");
		Td2IdentityResult td2IdentityResult2 = Identity("MPE", "", "1000");
		Check(td2IdentityResult2.Name == "Narew" && td2IdentityResult2.Carrier == "PKP Intercity S.A.", "Dokładny numer pasażerskiego może wskazać nazwę i przewoźnika bez składu.");
		Check(Identity("ROJ", "EN57-001", "91101").Name == "Kurpiak" && Identity("ROJ", "EN57-001", "91101").Operator == "KM", "Dokładny numer ma pierwszeństwo przed szeroką regułą prefiksu 911.");
		Check(Identity("ROJ", "EN57-001", "39501").Name == "SKA2" && Identity("ROJ", "EN57-001", "39501").Operator == "KMŁ", "Reguła prefiksu pasażerskiego stosuje nazwę i zmianę operatora jak SAZ.");
		Check(Identity("MPE", "").Carrier == "" && Identity("MPE", "").Name == "", "Bez numeru z katalogu i składu pola pozostają puste.");
		Check(Td2Identity.CarrierCaption("DB") == "DB" && Td2Identity.CarrierCaption("LOTOS Kolej Sp. z o.o.").StartsWith("LOTOS"), "Prezentacja TD2 nie zamienia DB w DB Cargo ani LOTOS w nazwę z reguł SimRail.");
		Td2Record td2Record = Record("1000", "MPE", "ET22-468");
		td2Record.Schedule.trainInfo.carrierName = "<b>Przewoźnik z API</b>";
		td2Record.Schedule.trainInfo.trainName = "Nazwa z API";
		Td2IdentityResult td2IdentityResult3 = Td2Identity.Resolve(td2Record);
		Check(td2IdentityResult3.Carrier == "Przewoźnik z API" && td2IdentityResult3.Name == "Nazwa z API" && td2IdentityResult3.CarrierSource == "API TD2", "Jawne pola API TD2 pozostają nadrzędne wobec wnioskowania.");
	}

	private static Td2Schedule Schedule(int id = 1001)
	{
		Td2Schedule td2Schedule = (Td2Schedule)typeof(Td2190Tests).GetMethod("Schedule", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[1] { id });
		td2Schedule.trainInfo.trainNo = "999999999";
		td2Schedule.trainInfo.trainCategoryCode = "TME";
		td2Schedule.trainInfo.driverId = "11";
		return td2Schedule;
	}

	private static byte[] Payload(params Td2Schedule[] schedules)
	{
		Td2Envelope td2Envelope = new Td2Envelope();
		td2Envelope.success = true;
		td2Envelope.message = schedules.ToList();
		return Json.Bytes(td2Envelope);
	}

	private static List<Td2OnlineTrain> Online(string consist, string region = "eu", string id = "1001")
	{
		List<Td2OnlineTrain> list = new List<Td2OnlineTrain>();
		list.Add(new Td2OnlineTrain
		{
			trainNo = "999999999",
			region = region,
			timetableId = id,
			dataCon = consist
		});
		return list;
	}

	private static SimRailSnapshot Accept(Td2Provider p, List<Td2OnlineTrain> online, params Td2Schedule[] schedules)
	{
		SimRailSnapshot simRailSnapshot = p.Accept(Payload(schedules), "eu", new DateTimeOffset(2026, 10, 23, 22, 30, 0, TimeSpan.Zero), online);
		simRailSnapshot.DownloadedUtc = DateTime.UtcNow;
		return simRailSnapshot;
	}

	private static void OnlineChecks(string folder)
	{
		Td2Schedule td2Schedule = Schedule();
		Td2Provider p = new Td2Provider();
		SimRailSnapshot simRailSnapshot = Accept(p, Online("ET22-468"), td2Schedule);
		Check(Td2Identity.Resolve(simRailSnapshot.Td2.Records.Single()).Carrier.StartsWith("CTL"), "Skład online przypisuje się do zgodnego regionu i identyfikatora rozkładu.");
		Check(Accept(new Td2Provider(), Online("ET22-468", "cae"), Schedule()).Td2.Records.Single().RuntimeConsist == null, "Pociąg z innego regionu nie dostarcza przewoźnika.");
		List<Td2OnlineTrain> list = Online("ET22-468");
		list[0].isOnline = 0;
		Check(Accept(new Td2Provider(), list, Schedule()).Td2.Records.Single().RuntimeConsist == null, "Nieaktywny wpis online nie zasila nowego rozkładu.");
		Td2Schedule td2Schedule2 = Schedule();
		Td2Schedule td2Schedule3 = Schedule(1002);
		td2Schedule3.trainInfo.driverId = "22";
		List<Td2OnlineTrain> list2 = Online("ET22-468", "eu", "");
		Check(Accept(new Td2Provider(), list2, td2Schedule2, td2Schedule3).Td2.Records.All((Td2Record r) => r.RuntimeConsist == null), "Dwa aktywne rozkłady z tym samym numerem nie są rozstrzygane przypadkowo.");
		list2[0].driverId = "22";
		SimRailSnapshot simRailSnapshot2 = Accept(new Td2Provider(), list2, td2Schedule2, td2Schedule3);
		Check(simRailSnapshot2.Td2.Find("1002").RuntimeConsist == "ET22-468" && simRailSnapshot2.Td2.Find("1001").RuntimeConsist == null, "Jednoznaczny maszynista rozdziela dwa aktywne rozkłady o tym samym numerze.");
		List<Td2OnlineTrain> list3 = Online("ET22-468");
		list3.Add(Online("EU07-088")[0]);
		Check(Accept(new Td2Provider(), list3, Schedule()).Td2.Records.Single().RuntimeConsist == null, "Sprzeczne duplikaty online nie wybierają losowej lokomotywy.");
		simRailSnapshot = Accept(p, new List<Td2OnlineTrain>(), td2Schedule);
		Check(simRailSnapshot.Td2.Find("1001").RuntimeConsist == "ET22-468", "Brak chwilowego odczytu zachowuje ostatni dopasowany skład tego samego ID.");
		simRailSnapshot = Accept(p, new List<Td2OnlineTrain>());
		Check(simRailSnapshot.Td2.Find("1001").Removed && Td2Identity.Resolve(simRailSnapshot.Td2.Find("1001")).Carrier.StartsWith("CTL"), "Usunięty rozkład zachowuje rozpoznane metadane.");
		simRailSnapshot = Accept(p, Online("EU07-088", "eu", "1002"), Schedule(1002));
		Check(Td2Identity.Resolve(simRailSnapshot.Td2.Find("1001")).Carrier.StartsWith("CTL") && Td2Identity.Resolve(simRailSnapshot.Td2.Find("1002")).Carrier.StartsWith("Ecco"), "Nowy rozkład o tym samym numerze nie podmienia lokomotywy starego wpisu.");
		int timetables = 0;
		int onlineReads = 0;
		Td2Provider td2Provider = new Td2Provider((string address, CancellationToken token) =>
		{
			if (address == Td2Provider.Address("eu"))
			{
				Interlocked.Increment(ref timetables);
				return Payload(Schedule());
			}
			if (address == "https://api.td2.info.pl/?method=getTrainsOnline")
			{
				Interlocked.Increment(ref onlineReads);
				return Json.Bytes(new Td2OnlineEnvelope
				{
					success = true,
					message = Online("ET22-468")
				});
			}
			throw new InvalidOperationException("Nieoczekiwany endpoint");
		});
		SimRailSnapshot result = td2Provider.SnapshotAsync("PL1", CancellationToken.None).GetAwaiter().GetResult();
		Check(timetables == 1 && onlineReads == 1 && result.Td2.Records.Single().RuntimeConsist == "ET22-468", "Odświeżenie pobiera rozkład i opcjonalny skład przez dwa odczyty.");
		string[] array = new string[2]
		{
			Td2Provider.Address("eu"),
			"https://api.td2.info.pl/?method=getTrainsOnline"
		};
		foreach (string text in array)
		{
			HttpWebRequest httpWebRequest = Td2Provider.CreateRequest(text);
			Check(httpWebRequest.Method == "GET" && string.IsNullOrEmpty(httpWebRequest.UserAgent) && !httpWebRequest.Headers.AllKeys.Any((string k) => k.Equals("User-Agent", StringComparison.OrdinalIgnoreCase)), "GET bez User-Agent: " + text);
			httpWebRequest.Abort();
		}
		Td2Provider td2Provider2 = new Td2Provider((string address, CancellationToken token) =>
		{
			if (address == "https://api.td2.info.pl/?method=getTrainsOnline")
			{
				throw new IOException("Brak danych składu");
			}
			return Payload(Schedule());
		});
		SimRailSnapshot result2 = td2Provider2.SnapshotAsync("eu", CancellationToken.None).GetAwaiter().GetResult();
		Check(result2.Td2.Records.Count == 1 && Td2Identity.Resolve(result2.Td2.Records[0]).Carrier == "", "Awaria opcjonalnego odczytu nie blokuje rozkładu i nie wymyśla przewoźnika.");
		using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource())
		{
			cancellationTokenSource.Cancel();
			bool ok = false;
			try
			{
				td2Provider.SnapshotAsync("eu", cancellationTokenSource.Token).GetAwaiter().GetResult();
			}
			catch (OperationCanceledException)
			{
				ok = true;
			}
			Check(ok, "Anulowanie pobierania pozostaje anulowaniem operacji.");
		}
		Td2Schedule td2Schedule4 = Schedule();
		td2Schedule4.trainInfo.carrierName = "Operator API";
		td2Schedule4.trainInfo.trainName = "Pociąg API";
		Td2Record record = Accept(new Td2Provider(), Online("ET22-468"), td2Schedule4).Td2.Records.Single();
		Check(Td2Identity.Resolve(record).Carrier == "Operator API" && Td2Identity.Resolve(record).Name == "Pociąg API", "Skład online nie nadpisuje jawnych danych przewoźnika i nazwy z rozkładu.");
		Td2Schedule td2Schedule5 = Schedule();
		td2Schedule5.trainInfo.trainNo = "1000";
		td2Schedule5.trainInfo.trainCategoryCode = "MPE";
		Td2Provider td2Provider3 = new Td2Provider();
		SimRailSnapshot simRailSnapshot3 = Accept(td2Provider3, null, td2Schedule5);
		Engine engine = (Engine)typeof(Td2190Tests).GetMethod("EngineFor", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[3] { folder, td2Provider3, simRailSnapshot3 });
		Check(engine.State.Trains.Where((Train t) => t.StationId == engine.StationId).All((Train t) => t.Carrier == "PKP Intercity S.A." && t.Profile.Name == "Narew" && t.Kind == "MPE"), "Projekcja TD2 uzupełnia nazwę i przewoźnika, zachowując ruchowy rodzaj MPE.");
		using MainForm mainForm = new MainForm(engine);
		mainForm.Show();
		Pump();
		DataGridView dataGridView = Field<DataGridView>(mainForm, "trains");
		Check(dataGridView.Rows.Cast<DataGridViewRow>().Any((DataGridViewRow r) => Convert.ToString(r.Cells["carrier"].Value) == "PKP Intercity S.A." && r.Cells["carrier"].ToolTipText.Contains("SAZ")), "Wykaz pokazuje przewoźnika i źródło jego rozpoznania w podpowiedzi.");
		Ui.Capture(mainForm, Path.Combine(folder, "td2-przewoznik-nazwa.png"));
		engine.Store.Commit((AppState state) =>
		{
			foreach (Train item in state.Trains.Where((Train t) => t.StationId == engine.StationId))
			{
				item.Carrier = "DB";
			}
		});
		mainForm.RefreshData();
		Pump();
		Check(Field<Label>(mainForm, "filterInfo").Text.Contains("DB") && !Field<Label>(mainForm, "filterInfo").Text.Contains("DB Cargo"), "Podsumowanie wykazu TD2 zachowuje pasażerskie DB bez aliasu towarowego.");
		Field<TabControl>(mainForm, "topTabs").SelectedIndex = 1;
		Pump();
		Check(!Field<Label>(mainForm, "filterInfo").Text.Contains("DB Cargo"), "Przejście do opisu pociągu nie przywraca aliasu z SimRail.");
		mainForm.Close();
		Pump();
	}

	private static void Tracks(string folder)
	{
		Engine engine = (Engine)typeof(Input196Tests).GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[1] { folder });
		JournalEntry entry = engine.Note(engine.Station.Sections.First().Id, engine.Now, "", "Pociąg");
		entry.Direction = "Z";
		entry.Number = "41174";
		engine.SaveJournal(entry);
		using MainForm mainForm = new MainForm(engine);
		mainForm.Show();
		mainForm.Hide();
		mainForm.Show();
		Pump();
		JournalPane journalPane = Field<List<JournalPane>>(mainForm, "panes").Single((JournalPane p) => p.SectionId == entry.SectionId);
		journalPane.FocusEntry(entry.Id);
		ReferenceGrid referenceGrid = (ReferenceGrid)journalPane.Grid;
		DataGridViewRow dataGridViewRow = referenceGrid.Rows.Cast<DataGridViewRow>().Single((DataGridViewRow r) => ((JournalEntry)r.Tag).Id == entry.Id);
		referenceGrid.CurrentCell = dataGridViewRow.Cells["track"];
		if (!referenceGrid.IsCurrentCellInEditMode)
		{
			referenceGrid.BeginEdit(selectAll: false);
		}
		Pump();
		StationTrackEditingControl stationTrackEditingControl = (StationTrackEditingControl)referenceGrid.EditingControl;
		stationTrackEditingControl.DroppedDown = false;
		Check(stationTrackEditingControl.DropDownStyle == ComboBoxStyle.DropDown && stationTrackEditingControl.BackColor == SystemColors.Window && stationTrackEditingControl.ForeColor == SystemColors.WindowText, "Tor można wpisywać ręcznie, a tło edytora jest białe mimo wymaganej niebieskiej komórki.");
		ComboInfo info = new ComboInfo
		{
			Size = Marshal.SizeOf(typeof(ComboInfo))
		};
		Check(GetComboBoxInfo(stationTrackEditingControl.Handle, ref info) && info.Edit != IntPtr.Zero, "Edytor toru ma natywne pole tekstowe.");
		stationTrackEditingControl.SelectAll();
		SendMessage(info.Edit, 258, new IntPtr(50), IntPtr.Zero);
		Pump();
		Check(stationTrackEditingControl.Text == "2" && referenceGrid.EndEdit() && journalPane.CommitPending() && engine.State.Entries.Single((JournalEntry e) => e.Id == entry.Id).Track == "2", "Wpisanie cyfry klawiaturą zapisuje numer toru w dzienniku.");
		referenceGrid.CurrentCell = dataGridViewRow.Cells["track"];
		referenceGrid.BeginEdit(selectAll: true);
		stationTrackEditingControl = (StationTrackEditingControl)referenceGrid.EditingControl;
		stationTrackEditingControl.DroppedDown = false;
		stationTrackEditingControl.Text = "3";
		Check(referenceGrid.EndEdit() && journalPane.CommitPending() && engine.TrackStatus("3").Contains("41174"), "Ręczne wpisanie numeru aktualizuje rezerwację toru.");
		referenceGrid.CurrentCell = dataGridViewRow.Cells["track"];
		referenceGrid.BeginEdit(selectAll: true);
		stationTrackEditingControl = (StationTrackEditingControl)referenceGrid.EditingControl;
		stationTrackEditingControl.DroppedDown = false;
		stationTrackEditingControl.Text = "99";
		Check((!referenceGrid.EndEdit() || !journalPane.CommitPending()) && engine.State.Entries.Single((JournalEntry e) => e.Id == entry.Id).Track == "3", "Ręczne wpisanie nieistniejącego toru nie tworzy ukrytej definicji toru.");
		referenceGrid.BeginEdit(selectAll: true);
		stationTrackEditingControl = (StationTrackEditingControl)referenceGrid.EditingControl;
		stationTrackEditingControl.DroppedDown = false;
		stationTrackEditingControl.Text = "";
		Check(referenceGrid.EndEdit() && journalPane.CommitPending() && engine.TrackStatus("3") == "WOLNY", "Skasowanie tekstu usuwa przypisanie i zwalnia rezerwację.");
		using (StationTrackComboBox stationTrackComboBox = new StationTrackComboBox(engine, ""))
		{
			using Bitmap bitmap = new Bitmap(150, 35);
			using Graphics graphics = Graphics.FromImage(bitmap);
			stationTrackComboBox.BackColor = Color.FromArgb(154, 188, 221);
			stationTrackComboBox.Items.Add("test");
			typeof(StationTrackComboBox).GetMethod("OnDrawItem", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(stationTrackComboBox, new object[1]
			{
				new DrawItemEventArgs(graphics, stationTrackComboBox.Font, new Rectangle(0, 0, 150, 35), 1, DrawItemState.None)
			});
			Check(bitmap.GetPixel(149, 34).ToArgb() == SystemColors.Window.ToArgb(), "Niezaznaczona pozycja listy ma białe tło niezależne od koloru komórki.");
			bitmap.Save(Path.Combine(folder, "lista-torow-biale-tlo.png"));
		}
		using (StationTrackComboBox stationTrackComboBox2 = new StationTrackComboBox(engine, "1"))
		{
			Check(stationTrackComboBox2.DropDownStyle == ComboBoxStyle.DropDown, "Formularz przyjęcia / wyprawienia również pozwala wpisać numer toru.");
			stationTrackComboBox2.Text = "2";
			Check(stationTrackComboBox2.Text == "2", "Wpis w formularzu nie wymaga wybierania z listy.");
		}
		Ui.Capture(mainForm, Path.Combine(folder, "tor-stacyjny-edycja.png"));
		mainForm.Close();
		Pump();
	}

	public static bool Run(string folder)
	{
		Directory.CreateDirectory(folder);
		Ui.Testing = true;
		bool enabled = LocalPersistence.Enabled;
		LocalPersistence.Enabled = false;
		try
		{
			IdentityChecks();
			OnlineChecks(folder);
			Tracks(folder);
		}
		catch (Exception ex)
		{
			log.Add(ex.ToString());
			File.WriteAllLines(Path.Combine(folder, "wyniki-update199.txt"), log, Encoding.UTF8);
			return false;
		}
		finally
		{
			LocalPersistence.Enabled = enabled;
		}
		File.WriteAllLines(Path.Combine(folder, "wyniki-update199.txt"), log, Encoding.UTF8);
		return true;
	}

	public static bool Compare(string file)
	{
		List<string> list = new List<string>();
		try
		{
			List<Td2IdentityVector> list2 = Json.Read<List<Td2IdentityVector>>(File.ReadAllBytes(file));
			int num = 0;
			foreach (Td2IdentityVector item in list2)
			{
				Td2IdentityResult td2IdentityResult = Identity(item.Category, item.Consist, item.Number);
				if (td2IdentityResult.Operator != item.Operator || td2IdentityResult.Name != item.Name)
				{
					num++;
					if (num <= 20)
					{
						list.Add("FAIL: " + item.Number + " / " + item.Category + " / " + item.Consist + " => " + td2IdentityResult.Operator + " / " + td2IdentityResult.Name + "; SAZ: " + item.Operator + " / " + item.Name);
					}
				}
			}
			list.Insert(0, "Przypadki z oryginalnego SAZ: " + list2.Count + "; różnice: " + num);
			if (num == 0)
			{
				list.Add("PASS: Wszystkie " + list2.Count + " przypadki zwracają identyczny kod przewoźnika i nazwę jak SAZ.");
			}
			File.WriteAllLines(Path.Combine(Path.GetDirectoryName(file), "wyniki-saz199.txt"), list, Encoding.UTF8);
			return num == 0;
		}
		catch (Exception ex)
		{
			list.Add(ex.ToString());
			File.WriteAllLines(Path.Combine(Path.GetDirectoryName(file), "wyniki-saz199.txt"), list, Encoding.UTF8);
			return false;
		}
	}
}
