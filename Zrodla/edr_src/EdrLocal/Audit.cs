using System;

namespace EdrLocal;

public class Audit
{
	public string UserName { get; set; }

	public DateTime Time { get; set; }

	public string User { get; set; }

	public string Action { get; set; }

	public string Entity { get; set; }

	public string Before { get; set; }

	public string After { get; set; }
}
