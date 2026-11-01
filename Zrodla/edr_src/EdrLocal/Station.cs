using System.Collections.Generic;

namespace EdrLocal;

public class Station
{
	public List<Td2AreaPost> Td2AreaPosts { get; set; }

	public List<Td2AreaPost> Td2AreaCatalog { get; set; }

	public bool? Td2ShowRouteLines { get; set; }

	public string Td2SceneName { get; set; }

	public string Td2SceneId { get; set; }

	public string ConfiguratorRevision { get; set; }

	public int RouteNeighboursVersion { get; set; }

	public string PostType { get; set; }

	public int SimulatorTopologyVersion { get; set; }

	public StationOptions Options { get; set; }

	public int SimulatorIdentityVersion { get; set; }

	public bool SimulatorReviewed { get; set; }

	public string SimRailPointId { get; set; }

	public List<JournalTabDefinition> JournalTabs { get; set; }

	public List<StationTrackSetting> TrackSettings { get; set; }

	public List<RequiredCheckSetting> RequiredChecks { get; set; }

	public string Id { get; set; }

	public string Name { get; set; }

	public List<Section> Sections { get; set; }

	public List<string> Tracks { get; set; }

	public List<string> Checks { get; set; }
}
