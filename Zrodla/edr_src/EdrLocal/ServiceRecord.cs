using System;

namespace EdrLocal;

public class ServiceRecord
{
	public string Id { get; set; }

	public string StationId { get; set; }

	public string Type { get; set; }

	public string Train { get; set; }

	public string Carrier { get; set; }

	public string Track { get; set; }

	public DateTime Start { get; set; }

	public DateTime? End { get; set; }

	public string Notes { get; set; }

	public string Status { get; set; }
}
