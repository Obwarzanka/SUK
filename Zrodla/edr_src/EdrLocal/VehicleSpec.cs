namespace EdrLocal;

public sealed class VehicleSpec
{
	public string Type { get; set; }

	public string SepeSeries { get; set; }

	public decimal? Length { get; set; }

	public decimal? Gross { get; set; }

	public decimal? Axles { get; set; }

	public decimal? MaxSpeed { get; set; }

	public string Traction { get; set; }

	public int SourceRow { get; set; }
}
