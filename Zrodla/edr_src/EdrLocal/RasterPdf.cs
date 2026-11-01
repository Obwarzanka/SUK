using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace EdrLocal;

internal static class RasterPdf
{
	public static void Write(string path, JournalReport report)
	{
		LocalPersistence.RequireEnabled();
		FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write);
		try
		{
			Action<string> write = (string s) =>
			{
				byte[] bytes = Encoding.ASCII.GetBytes(s);
				stream.Write(bytes, 0, bytes.Length);
			};
			List<long> offsets = new List<long> { 0L };
			Action<int, string> action = (int id, string body) =>
			{
				offsets.Add(stream.Position);
				write(id + " 0 obj\n" + body + "\nendobj\n");
			};
			write("%PDF-1.4\n");
			action(1, "<< /Type /Catalog /Pages 2 0 R >>");
			action(2, "<< /Type /Pages /Count " + report.PageCount + " /Kids [" + string.Join(" ", from i in Enumerable.Range(0, report.PageCount)
				select 3 + i * 3 + " 0 R") + "] >>");
			for (int num = 0; num < report.PageCount; num++)
			{
				int num2 = 3 + num * 3;
				action(num2, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595.44 841.68] /Resources << /XObject << /Im0 " + (num2 + 2) + " 0 R >> >> /Contents " + (num2 + 1) + " 0 R >>");
				string text = "q\n595.44 0 0 841.68 0 0 cm\n/Im0 Do\nQ\n";
				action(num2 + 1, "<< /Length " + Encoding.ASCII.GetByteCount(text) + " >>\nstream\n" + text + "endstream");
				using Bitmap bitmap = report.RenderPage(num, 2);
				using MemoryStream memoryStream = new MemoryStream();
				bitmap.Save(memoryStream, ImageFormat.Jpeg);
				byte[] array = memoryStream.ToArray();
				offsets.Add(stream.Position);
				write(num2 + 2 + " 0 obj\n<< /Type /XObject /Subtype /Image /Width " + bitmap.Width + " /Height " + bitmap.Height + " /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length " + array.Length + " >>\nstream\n");
				stream.Write(array, 0, array.Length);
				write("\nendstream\nendobj\n");
			}
			long position = stream.Position;
			write("xref\n0 " + offsets.Count + "\n0000000000 65535 f \n");
			foreach (long item in offsets.Skip(1))
			{
				write(item.ToString("0000000000", CultureInfo.InvariantCulture) + " 00000 n \n");
			}
			write("trailer\n<< /Size " + offsets.Count + " /Root 1 0 R >>\nstartxref\n" + position + "\n%%EOF\n");
		}
		finally
		{
			if (stream != null)
			{
				((IDisposable)stream).Dispose();
			}
		}
	}
}
