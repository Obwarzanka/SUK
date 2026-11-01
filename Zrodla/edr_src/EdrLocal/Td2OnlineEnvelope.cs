using System.Collections.Generic;
using System.Runtime.Serialization;

namespace EdrLocal;

[DataContract]
public sealed class Td2OnlineEnvelope
{
	[DataMember(IsRequired = true)]
	public bool success;

	[DataMember]
	public List<Td2OnlineTrain> message;
}
