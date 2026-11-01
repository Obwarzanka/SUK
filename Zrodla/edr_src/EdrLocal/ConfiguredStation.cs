namespace EdrLocal;

public class ConfiguredStation
{
	public string Key { get; set; }

	public string UnitId { get; set; }

	public bool? Registered { get; set; }

	public Station Station { get; set; }

	public override string ToString()
	{
		return Station.Name;
	}
}
