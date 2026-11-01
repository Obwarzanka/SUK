using System;

namespace EdrLocal;

public class Duty
{
	public string Id { get; set; }

	public string StationId { get; set; }

	public string UserId { get; set; }

	public string UserName { get; set; }

	public DateTime Start { get; set; }

	public DateTime? End { get; set; }
}
