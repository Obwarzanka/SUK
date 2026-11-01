using System.Windows.Forms;

namespace EdrLocal;

internal static class GridExtensions
{
	public static void FrozenColumnCountCompat(this DataGridView g)
	{
		g.Columns[0].Frozen = true;
		g.Columns[1].Frozen = true;
	}
}
