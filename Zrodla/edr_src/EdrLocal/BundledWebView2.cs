using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace EdrLocal;

public static class BundledWebView2
{
	private static readonly object gate = new object();

	private static readonly Dictionary<string, Assembly> loaded = new Dictionary<string, Assembly>(StringComparer.Ordinal);

	private static bool initialized;

	public static string License => Encoding.UTF8.GetString(Resource("WebView2-LICENSE.txt"));

	internal static byte[] Resource(string name)
	{
		using Stream stream = typeof(BundledWebView2).Assembly.GetManifestResourceStream("EdrLocal.Dependencies." + name);
		if (stream == null)
		{
			throw new InvalidDataException("Brak dołączonego składnika " + name);
		}
		using MemoryStream memoryStream = new MemoryStream();
		stream.CopyTo(memoryStream);
		return memoryStream.ToArray();
	}

	private static Assembly Load(string name)
	{
		if (loaded.TryGetValue(name, out var value))
		{
			return value;
		}
		value = Assembly.Load(Resource(name + ".dll"));
		if (value.GetName().Name != name)
		{
			throw new InvalidDataException("Nieprawidłowy składnik " + name);
		}
		loaded.Add(name, value);
		return value;
	}

	private static Assembly Resolve(object sender, ResolveEventArgs args)
	{
		AssemblyName assemblyName = new AssemblyName(args.Name);
		if (!(assemblyName.Name != "Microsoft.Web.WebView2.Core") || !(assemblyName.Name != "Microsoft.Web.WebView2.WinForms"))
		{
			lock (gate)
			{
				Assembly assembly = Load(assemblyName.Name);
				return AssemblyName.ReferenceMatchesDefinition(assemblyName, assembly.GetName()) ? assembly : null;
			}
		}
		return null;
	}

	public static void Initialize()
	{
		lock (gate)
		{
			if (!initialized)
			{
				AppDomain.CurrentDomain.AssemblyResolve += Resolve;
				Load("Microsoft.Web.WebView2.Core");
				Load("Microsoft.Web.WebView2.WinForms");
				initialized = true;
			}
		}
	}

	private static string Hash(byte[] bytes)
	{
		using SHA256 sHA = SHA256.Create();
		return BitConverter.ToString(sHA.ComputeHash(bytes)).Replace("-", "");
	}

	internal static string ExtractLoader(string root, string arch)
	{
		if (arch != "x64" && arch != "x86")
		{
			throw new ArgumentException("Nieobsługiwana architektura WebView2.");
		}
		byte[] bytes = Resource(arch + ".WebView2Loader.dll");
		string text = Hash(bytes);
		string text2 = Path.Combine(root, text, arch);
		string text3 = Path.Combine(text2, "WebView2Loader.dll");
		Directory.CreateDirectory(text2);
		if (File.Exists(text3) && Hash(File.ReadAllBytes(text3)) == text)
		{
			return text2;
		}
		string text4 = text3 + "." + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			File.WriteAllBytes(text4, bytes);
			try
			{
				if (File.Exists(text3))
				{
					File.Replace(text4, text3, null);
				}
				else
				{
					File.Move(text4, text3);
				}
			}
			catch (IOException)
			{
				if (!File.Exists(text3) || Hash(File.ReadAllBytes(text3)) != text)
				{
					throw;
				}
			}
		}
		finally
		{
			if (File.Exists(text4))
			{
				File.Delete(text4);
			}
		}
		if (Hash(File.ReadAllBytes(text3)) != text)
		{
			throw new InvalidDataException("Nie udało się przygotować składnika WebView2Loader.dll.");
		}
		return text2;
	}

	public static string LoaderFolder()
	{
		return ExtractLoader(Path.Combine(Path.GetTempPath(), "EDR-WebView2Loader"), Environment.Is64BitProcess ? "x64" : "x86");
	}
}
