namespace EdrLocal;

public sealed class Td2AreaPost
{
	public string Id { get; set; }

	public string Name { get; set; }

	public override string ToString()
	{
		return Name + " [" + Id + "]";
	}
}
