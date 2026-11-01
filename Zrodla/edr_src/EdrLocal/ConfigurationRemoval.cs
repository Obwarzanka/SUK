using System.Collections.Generic;

namespace EdrLocal;

public sealed class ConfigurationRemoval
{
	public string Caption { get; set; }

	public List<string> UnitIds { get; set; }

	public List<string> StationKeys { get; set; }

	public int SectionCount { get; set; }

	public int StationCount { get; set; }
}
