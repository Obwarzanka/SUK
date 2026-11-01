using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EdrLocal;

public interface ISimulatorFeed
{
	string ProviderId { get; }

	Task<List<SimRailServer>> ServersAsync(CancellationToken token);

	Task<SimRailSnapshot> SnapshotAsync(string server, CancellationToken token);
}
