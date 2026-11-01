using System.Collections.Generic;

namespace EdrLocal;

public sealed class Td2Schedule
{
	public Td2TrainInfo trainInfo { get; set; }

	public string timetablePath { get; set; }

	public List<Td2Stop> stopPoints { get; set; }
}
