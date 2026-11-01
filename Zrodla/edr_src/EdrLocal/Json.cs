using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace EdrLocal;

public static class Json
{
	public static byte[] Bytes<T>(T value)
	{
		using MemoryStream memoryStream = new MemoryStream();
		new DataContractJsonSerializer(typeof(T)).WriteObject((Stream)memoryStream, (object)value);
		return memoryStream.ToArray();
	}

	public static T Read<T>(byte[] bytes)
	{
		using MemoryStream stream = new MemoryStream(bytes);
		return (T)new DataContractJsonSerializer(typeof(T)).ReadObject((Stream)stream);
	}

	public static T Clone<T>(T value)
	{
		return Read<T>(Bytes(value));
	}

	public static string Text<T>(T value)
	{
		return Encoding.UTF8.GetString(Bytes(value));
	}
}
