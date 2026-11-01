using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EdrLocal;

internal sealed class SessionWindows : ApplicationContext
{
	private readonly List<MainForm> windows = new List<MainForm>();

	private readonly Store store;

	private bool linkChanging;

	private readonly List<MainForm> retiring = new List<MainForm>();

	private readonly HashSet<string> pausedWaiting = new HashSet<string>();

	private DialogResult result;

	private readonly Dictionary<string, List<SepeWriteRequest>> waiting = new Dictionary<string, List<SepeWriteRequest>>();

	internal int Count => windows.Count;

	internal SessionWindows(MainForm root)
	{
		store = root.Engine.Store;
		store.Committed += Changed;
		Attach(root);
	}

	internal static DialogResult Run(MainForm root)
	{
		using SessionWindows sessionWindows = root.sessionWindows ?? new SessionWindows(root);
		root.Show();
		Application.Run(sessionWindows);
		return sessionWindows.result;
	}

	private void Attach(MainForm view)
	{
		windows.Add(view);
		view.sessionWindows = this;
		view.windowStationSignature = Json.Text(view.Engine.Station);
		view.FormClosed += (object s, FormClosedEventArgs e) =>
		{
			Remove(view);
		};
	}

	internal MainForm Open(MainForm source)
	{
		MainForm next = new MainForm(source.Engine.CreateWindowView())
		{
			additionalSessionView = true
		};
		Attach(next);
		next.Shown += (object s, EventArgs e) =>
		{
			source.CopyWindowView(next);
		};
		next.Show();
		return next;
	}

	internal bool Busy(MainForm except)
	{
		if (!linkChanging && !windows.Any((MainForm x) => x != except && !x.IsDisposed && (x.sendingHours || x.simulatorRefreshing)))
		{
			return retiring.Any((MainForm x) => x.sendingHours || x.simulatorRefreshing || (x.hoursTask != null && !x.hoursTask.IsCompleted));
		}
		return true;
	}

	private void Changed()
	{
		MainForm[] array = windows.ToArray();
		foreach (MainForm mainForm in array)
		{
			mainForm.QueueSharedRefresh();
		}
	}

	internal void Logout()
	{
		result = DialogResult.Retry;
		MainForm[] array = windows.ToArray();
		foreach (MainForm mainForm in array)
		{
			if (!mainForm.IsDisposed)
			{
				mainForm.Close();
			}
		}
		if (windows.Count > 0)
		{
			result = DialogResult.None;
		}
	}

	private async void Remove(MainForm view)
	{
		windows.Remove(view);
		if (windows.Count == 0)
		{
			store.Committed -= Changed;
			ExitThread();
			return;
		}
		retiring.Add(view);
		try
		{
			if (view.hoursTask != null)
			{
				try
				{
					await view.hoursTask;
				}
				catch
				{
				}
			}
			while (view.simulatorRefreshing && windows.Count > 0)
			{
				await Task.Delay(25);
			}
		}
		finally
		{
			retiring.Remove(view);
		}
		if (windows.Count != 0)
		{
			if (view.hoursPaused && view.outgoingHours.Count > 0)
			{
				pausedWaiting.Add(view.Engine.StationId);
			}
			if (!waiting.TryGetValue(view.Engine.StationId, out var pending))
			{
				pending = new List<SepeWriteRequest>();
				waiting[view.Engine.StationId] = pending;
			}
			pending.AddRange(view.outgoingHours);
			view.outgoingHours.Clear();
			MainForm target = windows.FirstOrDefault((MainForm x) => !x.IsDisposed && x.Engine.StationId == view.Engine.StationId);
			if (target != null)
			{
				ResumeOrphans(target);
			}
		}
	}

	internal void ResumeOrphans(MainForm view)
	{
		if (!waiting.TryGetValue(view.Engine.StationId, out var value))
		{
			return;
		}
		waiting.Remove(view.Engine.StationId);
		if (pausedWaiting.Remove(view.Engine.StationId))
		{
			view.hoursPaused = true;
		}
		foreach (SepeWriteRequest item in value)
		{
			view.outgoingHours.Enqueue(item);
		}
		if (!view.hoursPaused && view.IsHandleCreated)
		{
			view.BeginInvoke(new Action(view.ProcessSepeWrites));
		}
	}

	internal async Task ReplaceLink(MainForm source, string value)
	{
		source.ValidateReplacementLink(value);
		if (linkChanging)
		{
			throw new InvalidOperationException("Trwa zmiana linku SEPE.");
		}
		MainForm[] all = windows.Where((MainForm x) => !x.IsDisposed).ToArray();
		if (all.Any((MainForm x) => x.sendingHours && (x.hoursTask == null || x.hoursTask.IsCompleted)))
		{
			throw new InvalidOperationException("Poczekaj na zakończenie operacji Pociąg STÓJ / prognozy.");
		}
		linkChanging = true;
		try
		{
			MainForm[] array = all;
			foreach (MainForm mainForm in array)
			{
				mainForm.hoursPaused = true;
				if (mainForm.hoursCancellation != null)
				{
					mainForm.hoursCancellation.Cancel();
				}
				if (mainForm.backgroundWriter != null)
				{
					mainForm.backgroundWriter.Dispose();
					mainForm.backgroundWriter = null;
				}
			}
			int num2 = default;
			if (num2 == 0)
			{
				goto IL_01bb;
			}
			MainForm[] array2 = all;
			int num3 = 0;
			goto IL_01d8;
			IL_01d8:
			MainForm view = default;
			if (num3 < array2.Length)
			{
				view = array2[num3];
				if (view.hoursTask != null)
				{
					goto IL_01bb;
				}
				goto IL_01ca;
			}
			if (source.IsDisposed || source.closingSession)
			{
				return;
			}
			source.Engine.SimRail.SetAccessLink(value);
			source.Engine.EnsureControlledStations();
			MainForm[] array3 = all;
			foreach (MainForm mainForm2 in array3)
			{
				if (!mainForm2.IsDisposed && mainForm2.simulatorState != null)
				{
					mainForm2.simulatorState.Text = "SEPE: link zmieniony. Użyj Wyślij oczekujące godziny.";
				}
			}
			Changed();
			return;
			IL_01ca:
			num3++;
			goto IL_01d8;
			IL_01bb:
			await view.hoursTask;
			goto IL_01ca;
		}
		finally
		{
			linkChanging = false;
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			store.Committed -= Changed;
		}
		base.Dispose(disposing);
	}
}
