using System;
using System.Threading;
using System.Threading.Tasks;

namespace EdrLocal;

public static class SepeConfirmation
{
	public static async Task<SepeConfirmationResult> Read(SepeWriteRequest request, SimRailSession session, Action<SimRailSnapshot> accept, Action<int> progress, CancellationToken token, int pause = 1500)
	{
		string reason = "API nie potwierdziło wpisanych godzin.";
		for (int i = 1; i <= 3; i++)
		{
			token.ThrowIfCancellationRequested();
			progress?.Invoke(i);
			await Task.Delay(pause, token);
			SimRailSnapshot snapshot = null;
			try
			{
				snapshot = await session.Feed.SnapshotAsync(session.ServerCode, token);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception error)
			{
				reason = SepeDiagnostics.Reason(error);
			}
			if (snapshot == null)
			{
				continue;
			}
			try
			{
				accept?.Invoke(snapshot);
				if (request.Confirmed(snapshot, session.PointId))
				{
					SepeConfirmationResult sepeConfirmationResult = new SepeConfirmationResult();
					sepeConfirmationResult.Confirmed = true;
					return sepeConfirmationResult;
				}
				reason = "API nie potwierdziło wpisanych godzin.";
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception error2)
			{
				reason = SepeDiagnostics.Reason(error2);
			}
		}
		return new SepeConfirmationResult
		{
			Reason = "Brak potwierdzenia po 3 odczytach. " + reason
		};
	}
}
