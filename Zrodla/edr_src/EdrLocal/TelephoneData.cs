using System;

namespace EdrLocal;

public class TelephoneData
{
	public string TemplateId { get; set; }

	public bool Incoming { get; set; }

	public DateTime SentAt { get; set; }

	public DateTime EffectiveAt { get; set; }

	public int Track { get; set; }

	public string LocalStation { get; set; }

	public string NeighbourStation { get; set; }

	public string LocalParty { get; set; }

	public string Sender { get; set; }

	public string Receiver { get; set; }

	public string Reason { get; set; }

	public string ClosureMode { get; set; }

	public bool ForPurpose { get; set; }

	public string WorkSupervisorName { get; set; }

	public string WorkSupervisorRole { get; set; }

	public string SpeedRestrictions { get; set; }

	public string PassingTrainNumber { get; set; }

	public string TrainNumber { get; set; }

	public bool Passed { get; set; }

	public string Notice { get; set; }

	public DateTime? LastTrainDeparture { get; set; }

	public string AdditionalNote { get; set; }

	public string FreeText { get; set; }
}
