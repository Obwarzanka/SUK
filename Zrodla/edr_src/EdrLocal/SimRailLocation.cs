namespace EdrLocal;

public class SimRailLocation
{
	public string SceneName { get; set; }

	public string SceneId { get; set; }

	public string Id { get; set; }

	public string Name { get; set; }

	public override string ToString()
	{
		return (string.IsNullOrEmpty(SceneName) ? Name : (SceneName + " / " + Name)) + " [" + Id + "]";
	}
}
