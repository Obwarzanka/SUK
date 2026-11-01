using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace EdrLocal;

public static class SepeChronologyRetry
{
	public const int Interval = 5000;

	public const int Duration = 30000;

	private static Exception Expired(Exception last)
	{
		return new InvalidOperationException("Nie udało się wysłać godzin przez 30 sekund. Ponawiano co 5 sekund.\r\nOstatni komunikat: " + SepeDiagnostics.Reason(last));
	}

	public static async Task Run(Func<int, CancellationToken, Task> attempt, Action<int, int> waiting, CancellationToken token, Func<int, CancellationToken, Task> delay = null, Func<long> milliseconds = null)
	{
		Stopwatch watch = Stopwatch.StartNew();
		if (milliseconds == null)
		{
			milliseconds = () => watch.ElapsedMilliseconds;
		}
		if (delay == null)
		{
			delay = (int duration, CancellationToken cancel) => Task.Delay(duration, cancel);
		}
		CancellationTokenSource deadline = null;
		long started = 0L;
		Exception last = null;
		int number = 0;
		try
		{
			do
			{
				token.ThrowIfCancellationRequested();
				CancellationToken current = deadline?.Token ?? token;
				try
				{
					int arg;
					number = (arg = number + 1);
					await attempt(arg, current);
					current.ThrowIfCancellationRequested();
					return;
				}
				catch (SepeChronologyException ex)
				{
					last = ex;
					if (deadline == null)
					{
						started = milliseconds();
						deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
						deadline.CancelAfter(30000);
					}
				}
				catch (OperationCanceledException)
				{
					if (token.IsCancellationRequested)
					{
						throw;
					}
					if (deadline != null && deadline.IsCancellationRequested)
					{
						throw Expired(last);
					}
					throw;
				}
				int remaining = (int)Math.Max(0L, 30000 - (milliseconds() - started));
				if (remaining == 0)
				{
					throw Expired(last);
				}
				int pause = Math.Min(5000, remaining);
				waiting?.Invoke((pause + 999) / 1000, (remaining + 999) / 1000);
				try
				{
					await delay(pause, deadline.Token);
				}
				catch (OperationCanceledException)
				{
					if (token.IsCancellationRequested)
					{
						throw;
					}
					throw Expired(last);
				}
			}
			while (milliseconds() - started < 30000);
			throw Expired(last);
		}
		finally
		{
			deadline?.Dispose();
		}
	}
}
