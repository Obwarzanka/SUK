using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EdrLocal;

public static class SepeRetry
{
	public static async Task Run(Func<int, Task> attempt, Func<bool> repeat, Action<int> progress, CancellationToken token, int pause = 500)
	{
		List<string> failures = new List<string>();
		for (int number = 1; number <= 3; number++)
		{
			token.ThrowIfCancellationRequested();
			progress?.Invoke(number);
			try
			{
				await attempt(number);
				break;
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (TrainTimeException)
			{
				throw;
			}
			catch (SepeChronologyException)
			{
				throw;
			}
			catch (Exception error)
			{
				SepeEnvironmentException ex4 = SepeEnvironmentException.From(error);
				if (ex4 != null)
				{
					throw ex4;
				}
				failures.Add("Próba " + number + ": " + SepeDiagnostics.Reason(error));
				if (!repeat())
				{
					throw;
				}
				if (number == 3)
				{
					throw new InvalidOperationException("Nie udało się przygotować zapisu SEPE po 3 próbach.\r\n\r\n" + string.Join("\r\n", failures));
				}
			}
			await Task.Delay(pause, token);
		}
	}
}
