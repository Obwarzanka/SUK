using System.Threading;
using System.Threading.Tasks;

namespace EdrLocal;

public interface IAccidentCounterFeed
{
	Task<AccidentCounterReading> FetchAsync(string simulator, CancellationToken token);
}
