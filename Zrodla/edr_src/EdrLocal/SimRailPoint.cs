namespace EdrLocal;

public class SimRailPoint
{
	public int indexOfPoint { get; set; }

	public string nameForPerson { get; set; }

	public string pointId { get; set; }

	public string displayedTrainNumber { get; set; }

	public string arrivalTime { get; set; }

	public string actualArrivalTime { get; set; }

	public string departureTime { get; set; }

	public string actualDepartureTime { get; set; }

	public bool isStoped { get; set; }

	public double stopDuration { get; set; }

	public bool isActive { get; set; }

	public bool isConfirmed { get; set; }

	public int confirmedBy { get; set; }

	public double plannedStop { get; set; }

	public int stopTypeNumber { get; set; }

	public bool leftTrack { get; set; }

	public object line { get; set; }

	public string platform { get; set; }

	public object track { get; set; }

	public string trainType { get; set; }

	public double mileage { get; set; }

	public int maxSpeed { get; set; }
}
