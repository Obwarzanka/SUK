using System;
using System.Collections.Generic;

namespace EdrLocal;

public class ConfigurationCatalogCache
{
	public int SchemaVersion { get; set; }

	public string ServerCode { get; set; }

	public DateTime SavedUtc { get; set; }

	public List<ConfiguredStation> Stations { get; set; }
}
