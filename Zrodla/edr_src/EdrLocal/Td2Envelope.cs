using System.Collections.Generic;
using System.Runtime.Serialization;

namespace EdrLocal;

[DataContract]
public sealed class Td2Envelope
{
	[DataMember(IsRequired = true)]
	public bool success;

	[DataMember]
	public int respCode;

	[DataMember(IsRequired = true)]
	public List<Td2Schedule> message;
}
