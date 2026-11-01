using System.Runtime.Serialization;

namespace EdrLocal;

[DataContract]
public sealed class AccidentCounterReading
{
	[DataMember(Name = "schemaVersion", IsRequired = true)]
	public int SchemaVersion;

	[DataMember(Name = "simulator", IsRequired = true)]
	public string Simulator;

	[DataMember(Name = "serverEpochSeconds", IsRequired = true)]
	public long ServerEpochSeconds;

	[DataMember(Name = "lastResetEpochSeconds", IsRequired = true)]
	public long LastResetEpochSeconds;

	[DataMember(Name = "revision", IsRequired = true)]
	public long Revision;
}
