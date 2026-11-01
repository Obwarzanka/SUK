using System;
using System.Collections.Generic;

namespace EdrLocal;

public class JournalEntry
{
	public int? TelephoneOrdinal { get; set; }

	public string CheckName { get; set; }

	public List<CrossingNotification> CrossingNotifications { get; set; }

	public bool ReturnFromLine { get; set; }

	public string ReturnOfId { get; set; }

	public DateTime? ReturnArrival { get; set; }

	public TelephoneData Telephone { get; set; }

	public string Id { get; set; }

	public string StationId { get; set; }

	public string SectionId { get; set; }

	public string TrainId { get; set; }

	public string Number { get; set; }

	public string Type { get; set; }

	public string Direction { get; set; }

	public string Track { get; set; }

	public int LineTrack { get; set; }

	public DateTime Time { get; set; }

	public DateTime? Request { get; set; }

	public DateTime? Permission { get; set; }

	public DateTime? LineIndicationStartedAt { get; set; }

	public DateTime? Left { get; set; }

	public DateTime? Arrived { get; set; }

	public string Text { get; set; }

	public string User { get; set; }

	public string Recipient { get; set; }

	public string Notification { get; set; }

	public string Effect { get; set; }

	public bool Cancelled { get; set; }

	public string CancelReason { get; set; }
}
