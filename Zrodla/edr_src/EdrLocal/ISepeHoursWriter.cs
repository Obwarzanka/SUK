using System;
using System.Threading.Tasks;

namespace EdrLocal;

public interface ISepeHoursWriter : IDisposable
{
	bool SubmissionAttempted { get; }

	Task Send(SepeWriteRequest request);
}
