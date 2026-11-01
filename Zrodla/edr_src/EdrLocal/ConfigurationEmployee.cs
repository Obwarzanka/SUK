namespace EdrLocal;

public class ConfigurationEmployee
{
	public string Id { get; set; }

	public string UnitId { get; set; }

	public string FirstName { get; set; }

	public string LastName { get; set; }

	public string Identifier { get; set; }

	public override string ToString()
	{
		return LastName + " " + FirstName + " (" + Identifier + ")";
	}
}
