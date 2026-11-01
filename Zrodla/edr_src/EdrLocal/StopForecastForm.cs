using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EdrLocal;

public sealed class StopForecastForm : ReferenceForm
{
	public readonly OperationTimePanel Arrival;

	public readonly OperationTimePanel Departure;

	public readonly Button StopButton;

	public readonly Button ForecastButton;

	public readonly Button CancelOperation;

	public readonly Label Status;

	private readonly Func<StopForecastOperation, DateTime, DateTime, Task> save;

	private readonly StopForecastOperation stopOperation;

	public bool Busy { get; private set; }

	public StopForecastForm(Train t, Func<StopForecastOperation, DateTime, DateTime, Task> submit)
	{
		save = submit;
		stopOperation = ((!t.Stopped) ? StopForecastOperation.Stop : StopForecastOperation.CancelStop);
		Text = "Postój (pociąg STÓJ) i prognoza przyjazdu / odjazdu";
		ClientSize = new Size(710, 693);
		StartPosition = FormStartPosition.CenterParent;
		FormBorderStyle = FormBorderStyle.FixedDialog;
		MaximizeBox = false;
		MinimizeBox = false;
		ShowInTaskbar = false;
		Font = ReferenceTheme.Dialog;
		Controls.Add(new Label
		{
			Text = "UWAGA! W tym oknie możliwa jest tylko obsługa \"Pociąg STÓJ\" i PROGNOZY Przyjazdu / Odjazdu.",
			ForeColor = Color.Red,
			Font = new Font(Font, FontStyle.Bold),
			TextAlign = ContentAlignment.MiddleCenter,
			Bounds = new Rectangle(8, 9, 694, 25)
		});
		Controls.Add(new Label
		{
			Text = "Pociąg " + t.Number + " / Relacja: " + t.Origin + " - " + t.Destination,
			ForeColor = Color.Green,
			Font = new Font("Segoe UI", 11f, FontStyle.Bold),
			TextAlign = ContentAlignment.MiddleCenter,
			Bounds = new Rectangle(10, 36, 690, 42)
		});
		Arrival = new OperationTimePanel("Przyjazd:", TrainPrediction.Arrival(t))
		{
			Bounds = new Rectangle(20, 83, 664, 265)
		};
		Departure = new OperationTimePanel("Odjazd:", TrainPrediction.Departure(t))
		{
			Bounds = new Rectangle(20, 357, 664, 265)
		};
		Controls.Add(Arrival);
		Controls.Add(Departure);
		SilverButton silverButton = new SilverButton
		{
			Text = "Przepisz czas",
			Bounds = new Rectangle(532, 213, 116, 36)
		};
		silverButton.Click += (object s, EventArgs e) =>
		{
			Departure.Value = Arrival.Value;
		};
		Arrival.Controls.Add(silverButton);
		Status = new Label
		{
			Bounds = new Rectangle(20, 624, 670, 23),
			ForeColor = Color.Navy,
			AutoEllipsis = true
		};
		Controls.Add(Status);
		StopButton = new SilverButton
		{
			Text = (t.Stopped ? "Odwołaj postój (pociąg STÓJ)" : "Wprowadź postój (pociąg STÓJ)"),
			Bounds = new Rectangle(20, 654, 292, 31)
		};
		ForecastButton = new SilverButton
		{
			Text = "Zapisz PROGNOZĘ",
			Bounds = new Rectangle(324, 654, 230, 31)
		};
		CancelOperation = new SilverButton
		{
			Text = "Anuluj",
			Bounds = new Rectangle(566, 654, 118, 31)
		};
		Controls.Add(StopButton);
		Controls.Add(ForecastButton);
		Controls.Add(CancelOperation);
		CancelOperation.Click += (object s, EventArgs e) =>
		{
			Close();
		};
		CancelButton = CancelOperation;
		StopButton.Click += async (object s, EventArgs e) =>
		{
			await Submit(stopOperation);
		};
		ForecastButton.Click += async (object s, EventArgs e) =>
		{
			await Submit(StopForecastOperation.Forecast);
		};
		FormClosing += (object s, FormClosingEventArgs e) =>
		{
			if (Busy)
			{
				e.Cancel = true;
			}
		};
	}

	private async Task Submit(StopForecastOperation operation)
	{
		if (Busy)
		{
			return;
		}
		bool forecast = operation == StopForecastOperation.Forecast;
		if (operation != StopForecastOperation.CancelStop && Arrival.Value > Departure.Value)
		{
			Status.Text = "Przyjazd nie może być późniejszy od odjazdu.";
			return;
		}
		Busy = true;
		OperationTimePanel arrival = Arrival;
		OperationTimePanel departure = Departure;
		Button stopButton = StopButton;
		Button forecastButton = ForecastButton;
		bool flag = (CancelOperation.Enabled = false);
		bool flag3 = (forecastButton.Enabled = flag);
		bool flag5 = (stopButton.Enabled = flag3);
		bool enabled = (departure.Enabled = flag5);
		arrival.Enabled = enabled;
		Label status = Status;
		string text;
		if (operation == StopForecastOperation.CancelStop)
		{
			text = "Odwoływanie postoju…";
		}
		else
		{
			text = (forecast ? "Zapisywanie prognozy…" : "Wprowadzanie postoju…");
		}
		status.Text = text;
		try
		{
			int num = default;
			_ = num;
			_ = 0;
			try
			{
				await save(operation, Arrival.Value, Departure.Value);
				Busy = false;
				DialogResult = DialogResult.OK;
				Close();
			}
			catch (ForecastDeclinedException ex)
			{
				Status.Text = ex.Message;
			}
			catch (Exception error)
			{
				Status.Text = SepeDiagnostics.Reason(error);
				if (!Ui.Testing)
				{
					MessageBox.Show(this, SepeDiagnostics.Reason(error), "SEPE", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				}
			}
		}
		finally
		{
			Busy = false;
			OperationTimePanel arrival2 = Arrival;
			OperationTimePanel departure2 = Departure;
			Button stopButton2 = StopButton;
			Button forecastButton2 = ForecastButton;
			bool flag8 = (CancelOperation.Enabled = true);
			bool flag10 = (forecastButton2.Enabled = flag8);
			bool flag12 = (stopButton2.Enabled = flag10);
			bool enabled2 = (departure2.Enabled = flag12);
			arrival2.Enabled = enabled2;
		}
	}
}
