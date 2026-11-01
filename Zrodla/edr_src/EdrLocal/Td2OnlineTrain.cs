using System;
using System.Linq;

namespace EdrLocal;

public sealed class Td2OnlineTrain
{
	public object trainNo { get; set; }

	public object timetableId { get; set; }

	public object driverId { get; set; }

	public object isOnline { get; set; }

	public string region { get; set; }

	public string server { get; set; }

	public string dataCon { get; set; }

	public string con { get; set; }

	public string vehicle { get; set; }

	public string locomotive { get; set; }

	public Td2TrainInfo trainInfo { get; set; }

	internal string Number => Td2Identity.Number(Td2Provider.Text(trainNo ?? ((trainInfo == null) ? null : trainInfo.trainNo)));

	internal string Timetable => Td2Provider.Text(timetableId ?? ((trainInfo == null) ? null : trainInfo.timetableId));

	internal string Driver => Td2Provider.Text(driverId ?? ((trainInfo == null) ? null : trainInfo.driverId));

	internal string Consist => new string[5]
	{
		dataCon,
		con,
		vehicle,
		locomotive,
		(trainInfo == null) ? null : trainInfo.dataCon
	}.FirstOrDefault((string s) => !string.IsNullOrWhiteSpace(s)) ?? "";

	internal string Region
	{
		get
		{
			string text = new string[3]
			{
				region,
				server,
				(trainInfo == null) ? null : trainInfo.region
			}.FirstOrDefault((string s) => !string.IsNullOrWhiteSpace(s));
			if (text == null)
			{
				return "";
			}
			try
			{
				return Td2Provider.Region(text);
			}
			catch (ArgumentException)
			{
				return "";
			}
		}
	}
}
