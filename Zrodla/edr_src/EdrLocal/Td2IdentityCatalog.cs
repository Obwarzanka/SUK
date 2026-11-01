using System.Collections.Generic;

namespace EdrLocal;

public sealed class Td2IdentityCatalog
{
	public Dictionary<string, string> OwnerNames { get; set; }

	public Dictionary<string, string> OwnerClasses { get; set; }

	public Dictionary<string, string[]> Operators { get; set; }

	public Dictionary<string, Dictionary<string, string[]>> WagonRules { get; set; }

	public Dictionary<string, Td2TractionRule> Traction { get; set; }

	public List<Td2TractionRule> Prefixes { get; set; }

	public List<Td2NumberOverride> Overrides { get; set; }

	public List<Td2NamedTrain> Trains { get; set; }
}
