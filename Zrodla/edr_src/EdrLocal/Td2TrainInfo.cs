namespace EdrLocal;

public sealed class Td2TrainInfo
{
	public object driverId { get; set; }

	public object timetableId { get; set; }

	public object trainNo { get; set; }

	public string trainCategoryCode { get; set; }

	public object trainMaxSpeed { get; set; }

	public string region { get; set; }

	public string driverName { get; set; }

	public object twr { get; set; }

	public object skr { get; set; }

	public object hasDangerousCargo { get; set; }

	public object hasExtraDeliveries { get; set; }

	public string warningNotes { get; set; }

	public string trainName { get; set; }

	public string carrierName { get; set; }

	public string dataCon { get; set; }

	public object dataLength { get; set; }

	public object dataMass { get; set; }
}
