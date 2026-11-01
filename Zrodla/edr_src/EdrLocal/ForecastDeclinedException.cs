using System;

namespace EdrLocal;

public sealed class ForecastDeclinedException : OperationCanceledException
{
	public ForecastDeclinedException()
		: base("Nie zapisano prognozy. Wybierz godzinę niepóźniejszą od bieżącej, aby zapisać sam STÓJ.")
	{
	}
}
