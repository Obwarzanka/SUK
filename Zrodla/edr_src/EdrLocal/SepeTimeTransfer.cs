using System;
using System.IO;
using System.Reflection;

namespace EdrLocal;

public static class SepeTimeTransfer
{
	public sealed class DomIdentity
	{
		public string Number { get; set; }

		public string PlanArrival { get; set; }

		public string PlanDeparture { get; set; }

		public string Origin { get; set; }

		public string Destination { get; set; }
	}

	private static string bridge;

	private static string Bridge
	{
		get
		{
			if (bridge == null)
			{
				using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EdrLocal.SepeBridge.js");
				using StreamReader streamReader = new StreamReader(stream);
				bridge = streamReader.ReadToEnd();
			}
			return bridge;
		}
	}

	private static string Values(DateTime? a, DateTime? d)
	{
		return "[" + Json.Text(a.HasValue ? a.Value.ToString("yyyy-MM-dd HH:mm") : null) + "," + Json.Text(d.HasValue ? d.Value.ToString("yyyy-MM-dd HH:mm") : null) + "]";
	}

	public static string Script(DateTime? a, DateTime? d, DateTime? day = null)
	{
		return Bridge + "\nwindow.EdrSepeBridge.prepare(" + Values(a, d) + "," + Json.Text((day ?? DateTime.Today).ToString("yyyy-MM-dd")) + ");";
	}

	public static string Verify(DateTime? a, DateTime? d, DateTime day)
	{
		return Bridge + "\nwindow.EdrSepeBridge.verify(" + Values(a, d) + "," + Json.Text(day.ToString("yyyy-MM-dd")) + ");";
	}

	public static string Submit(string train, DateTime? a, DateTime? d, DateTime day)
	{
		return Bridge + "\nwindow.EdrSepeBridge.submit(" + Json.Text(train) + "," + Values(a, d) + "," + Json.Text(day.ToString("yyyy-MM-dd")) + ");";
	}

	public static string StopRequest(SepeWriteRequest r, bool stopped = true, DateTime? arrival = null, DateTime? departure = null, DateTime? day = null)
	{
		return Bridge + "\nwindow.EdrSepeBridge.stopRequest(" + Request(r) + "," + (stopped ? "true" : "false") + "," + Values(arrival, departure) + "," + Json.Text((day ?? DateTime.Today).ToString("yyyy-MM-dd")) + ");";
	}

	public static string StopProgress(SepeWriteRequest r, bool stopped)
	{
		return Bridge + "\nwindow.EdrSepeBridge.stopProgress(" + Request(r) + "," + (stopped ? "true" : "false") + ");";
	}

	public static string OperationProgress(SepeWriteRequest r)
	{
		return Bridge + "\nwindow.EdrSepeBridge.operationProgress(" + Request(r) + ");";
	}

	public static string ForecastReply(SepeWriteRequest r, string message, bool accept)
	{
		return Bridge + "\nwindow.EdrSepeBridge.forecastReply(" + Request(r) + "," + Json.Text(message) + "," + (accept ? "true" : "false") + ");";
	}

	public static string State()
	{
		return Bridge + "\nwindow.EdrSepeBridge.state();";
	}

	public static string Errors()
	{
		return Bridge + "\nwindow.EdrSepeBridge.errors();";
	}

	public static string ReadTimes(DateTime day)
	{
		return Bridge + "\nwindow.EdrSepeBridge.readTimes(" + Json.Text(day.ToString("yyyy-MM-dd")) + ");";
	}

	public static string Validation(DateTime day)
	{
		return Bridge + "\nwindow.EdrSepeBridge.validation(" + Json.Text(day.ToString("yyyy-MM-dd")) + ");";
	}

	private static string Request(SepeWriteRequest r)
	{
		DomIdentity domIdentity = new DomIdentity();
		domIdentity.Number = r.Number;
		domIdentity.PlanArrival = r.PlanArrival.ToString("yyyy-MM-dd HH:mm");
		domIdentity.PlanDeparture = r.PlanDeparture.ToString("yyyy-MM-dd HH:mm");
		domIdentity.Origin = r.Origin;
		domIdentity.Destination = r.Destination;
		return Json.Text(domIdentity);
	}

	public static string Select(SepeWriteRequest r)
	{
		return Bridge + "\nwindow.EdrSepeBridge.select(" + Request(r) + ");";
	}

	public static string Identity(SepeWriteRequest r)
	{
		return Bridge + "\nwindow.EdrSepeBridge.identity(" + Request(r) + ");";
	}

	public static string Field(DateTime? a, DateTime? d, DateTime day, int field)
	{
		return Bridge + "\nwindow.EdrSepeBridge.field(" + Values(a, d) + "," + Json.Text(day.ToString("yyyy-MM-dd")) + "," + field + ");";
	}

	public static string SubmitRequest(SepeWriteRequest r, DateTime? a, DateTime? d, DateTime day)
	{
		return Bridge + "\nwindow.EdrSepeBridge.submitRequest(" + Request(r) + "," + Values(a, d) + "," + Json.Text(day.ToString("yyyy-MM-dd")) + ");";
	}
}
