namespace EdrLocal;

public class TrainProfile
{
	public string InternationalNumber { get; set; }

	public string Name { get; set; }

	public string OrderNumber { get; set; }

	public string Type { get; set; }

	public string TimetableType { get; set; }

	public string ApplicationNumber { get; set; }

	public string ApplicationDate { get; set; }

	public string Applicant { get; set; }

	public string Withdrawal { get; set; }

	public string WithdrawalDate { get; set; }

	public string Additional { get; set; }

	public string CarrierNotes { get; set; }

	public string OperationalNotes { get; set; }

	public string MaxWaiting { get; set; }

	public string ConnectionForecast { get; set; }

	public bool GaugeExceeded { get; set; }

	public bool LoadExceeded { get; set; }

	public bool Closure { get; set; }

	public bool Tracked { get; set; }
}
