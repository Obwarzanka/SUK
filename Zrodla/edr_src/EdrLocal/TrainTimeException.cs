using System;

namespace EdrLocal;

public sealed class TrainTimeException : InvalidOperationException
{
	public TrainTimeException(string message)
		: base(message)
	{
	}
}
