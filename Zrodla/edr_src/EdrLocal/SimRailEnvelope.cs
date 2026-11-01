using System.Collections.Generic;

namespace EdrLocal;

public class SimRailEnvelope<T>
{
	public bool result { get; set; }

	public List<T> data { get; set; }

	public string description { get; set; }
}
