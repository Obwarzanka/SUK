using System;
using System.Collections.Generic;

namespace EdrLocal;

public class ReportRow
{
	public List<ReportCell> Cells = new List<ReportCell>();

	public bool Previous;

	public bool Bold;

	public bool Strike;

	public bool Day;

	public DateTime Date;
}
