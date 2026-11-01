namespace EdrLocal;

public sealed class ConnectionOrganizationSelection
{
	public readonly string DepartmentId;

	public readonly string SectionId;

	public readonly bool Existing;

	public ConnectionOrganizationSelection(string department, string section, bool existing)
	{
		DepartmentId = department;
		SectionId = section;
		Existing = existing;
	}
}
