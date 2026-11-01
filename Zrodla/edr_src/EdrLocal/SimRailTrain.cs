using System;
using System.Collections.Generic;

namespace EdrLocal;

public class SimRailTrain
{
	public List<string> Vehicles { get; set; }

	public string VehicleRunId { get; set; }

	public bool VehiclesFromObservation { get; set; }

	public DateTime? VehiclesObservedUtc { get; set; }

	public bool VehicleMatchRejected { get; set; }

	public string locoType { get; set; }

	public decimal? trainLength { get; set; }

	public decimal? trainWeight { get; set; }

	public string trainNoLocal { get; set; }

	public string trainName { get; set; }

	public string startStation { get; set; }

	public string endStation { get; set; }

	public string usageNotes { get; set; }

	public string ownNotes { get; set; }

	public string carrierName { get; set; }

	public bool isQualityTracked { get; set; }

	public bool isOverGauge { get; set; }

	public bool? isOverWeight { get; set; }

	public bool isOtherExceptional { get; set; }

	public bool isHighRiskCargo { get; set; }

	public bool isDangerousCargo { get; set; }

	public List<SimRailPoint> timetable { get; set; }
}
