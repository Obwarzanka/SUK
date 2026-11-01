namespace EdrLocal;

public class ReportCell
{
	public int Column;

	public int Span;

	public string Text;

	public bool Left;

	public ReportCell(int column, int span, string text, bool left)
	{
		Column = column;
		Span = span;
		Text = text ?? "";
		Left = left;
	}
}
