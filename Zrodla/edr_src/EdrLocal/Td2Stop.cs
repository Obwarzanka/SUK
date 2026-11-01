namespace EdrLocal;

public sealed class Td2Stop
{
	public string pointId { get; set; }

	public object entryId { get; set; }

	public string pointName { get; set; }

	public string pointNameRAW { get; set; }

	public string pointNamePassenger { get; set; }

	public string arrivalLine { get; set; }

	public string departureLine { get; set; }

	public string arrivalTime { get; set; }

	public string departureTime { get; set; }

	public string arrivalRealTime { get; set; }

	public string departureRealTime { get; set; }

	public object arrivalDelay { get; set; }

	public object departureDelay { get; set; }

	public object confirmed { get; set; }

	public object isStopped { get; set; }

	public object pointStopTime { get; set; }

	public string pointStopType { get; set; }

	public object pointDistance { get; set; }

	public string comments { get; set; }

	public string platform { get; set; }

	public string track { get; set; }
}
