using System;
using System.Collections.Generic;

namespace EdrLocal;

public class Train
{
	public bool StartsHere { get; set; }

	public bool EndsHere { get; set; }

	public int? Td2FirstPointIndex { get; set; }

	public int? Td2LastPointIndex { get; set; }

	public bool Td2OutsideArea { get; set; }

	public string Td2IncomingPair { get; set; }

	public string Td2OutgoingPair { get; set; }

	public string Td2IncomingKey { get; set; }

	public string Td2OutgoingKey { get; set; }

	public string StopType { get; set; }

	public double? ExternalPlannedStop { get; set; }

	public string SourceProvider { get; set; }

	public string ExternalTrainNo { get; set; }

	public int ExternalPointIndex { get; set; }

	public bool ExternalConfirmed { get; set; }

	public int ExternalConfirmedBy { get; set; }

	public bool ExternalManualTimes { get; set; }

	public bool ExternalManualArrival { get; set; }

	public bool ExternalManualDeparture { get; set; }

	public bool LocalDepartureRecorded { get; set; }

	public bool LocalArrivalRecorded { get; set; }

	public bool RemovedFromSource { get; set; }

	public string Id { get; set; }

	public string StationId { get; set; }

	public string Number { get; set; }

	public string Kind { get; set; }

	public string From { get; set; }

	public string To { get; set; }

	public string Origin { get; set; }

	public string Destination { get; set; }

	public string Carrier { get; set; }

	public string CarrierSource { get; set; }

	public string LineIn { get; set; }

	public string LineOut { get; set; }

	public string Track { get; set; }

	public string Platform { get; set; }

	public DateTime PlanArrival { get; set; }

	public DateTime PlanDeparture { get; set; }

	public DateTime? Arrival { get; set; }

	public DateTime? Departure { get; set; }

	public DateTime? ArrivalForecast { get; set; }

	public DateTime? Forecast { get; set; }

	public bool Stopped { get; set; }

	public bool Runs { get; set; }

	public bool QualityTracked { get; set; }

	public bool Ready { get; set; }

	public bool Dangerous { get; set; }

	public bool HighRisk { get; set; }

	public bool Extraordinary { get; set; }

	public string Cargo { get; set; }

	public string Analysis { get; set; }

	public string Notes { get; set; }

	public string DelayReason { get; set; }

	public TrainConsist Consist { get; set; }

	public DateTime? ReadyReported { get; set; }

	public DateTime? ReadyRecordedAt { get; set; }

	public string ReadyDriver { get; set; }

	public TrainProfile Profile { get; set; }

	public List<DetailRow> DetailRows { get; set; }
}
