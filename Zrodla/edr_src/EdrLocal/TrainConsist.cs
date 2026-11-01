namespace EdrLocal;

public class TrainConsist
{
	public decimal? Gross { get; set; }

	public decimal? Length { get; set; }

	public decimal? PlannedGross { get; set; }

	public decimal? PlannedLength { get; set; }

	public bool ManualAnalysis { get; set; }

	public string[] VehicleTypes { get; set; }

	public string[] PlannedSeries { get; set; }

	public string[] Series { get; set; }

	public string[] Numbers { get; set; }
}
