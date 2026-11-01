using System.Collections.Generic;

namespace EdrLocal;

public sealed class PlannedTrain
{
	public string trainNoLocal { get; set; }

	public string trainName { get; set; }

	public string startStation { get; set; }

	public string endStation { get; set; }

	public string locoType { get; set; }

	public decimal? trainLength { get; set; }

	public decimal? trainWeight { get; set; }

	public List<SimRailPoint> timetable { get; set; }
}
