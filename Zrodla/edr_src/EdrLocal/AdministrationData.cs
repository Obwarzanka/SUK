using System.Collections.Generic;

namespace EdrLocal;

public class AdministrationData
{
	public bool DefaultStructureInitialized { get; set; }

	public string DefaultSectionId { get; set; }

	public List<string> ExpandedNodes { get; set; }

	public string SelectedNode { get; set; }

	public int SchemaVersion { get; set; }

	public string Provider { get; set; }

	public List<ConfigurationUnit> Units { get; set; }

	public List<ConfigurationEmployee> Employees { get; set; }

	public List<ConfigurationAssignment> Assignments { get; set; }

	public List<ConfiguredStation> Stations { get; set; }
}
