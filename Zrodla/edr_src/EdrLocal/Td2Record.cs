using System.Linq;

namespace EdrLocal;

public sealed class Td2Record
{
	public Td2Schedule Schedule;

	public bool Removed;

	public string RuntimeConsist;

	public bool Completed
	{
		get
		{
			Td2Stop td2Stop = Schedule.stopPoints.LastOrDefault((Td2Stop x) => !Td2Names.Sbl(x));
			if (td2Stop != null && Td2Provider.Flag(td2Stop.confirmed))
			{
				return Td2Provider.Time(td2Stop.arrivalRealTime).HasValue;
			}
			return false;
		}
	}

	public bool Runs
	{
		get
		{
			if (Removed)
			{
				return Completed;
			}
			return true;
		}
	}

	public string Id => Td2Provider.Text(Schedule.trainInfo.timetableId);
}
