namespace EdrLocal;

public class SimRailServer
{
	public string ServerCode { get; set; }

	public string ServerName { get; set; }

	public bool IsActive { get; set; }

	public override string ToString()
	{
		return ServerName ?? ServerCode;
	}
}
