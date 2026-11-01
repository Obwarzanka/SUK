using System.Collections.Generic;

namespace EdrLocal;

public sealed class SimRailRunningTrain
{
	public string TrainNoLocal { get; set; }

	public string ServerCode { get; set; }

	public string RunId { get; set; }

	public string TrainName { get; set; }

	public string StartStation { get; set; }

	public string EndStation { get; set; }

	public List<string> Vehicles { get; set; }
}
