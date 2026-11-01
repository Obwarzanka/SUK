using System.Drawing;

namespace EdrLocal;

public static class RouteAppearance
{
	public static Color Header(int n)
	{
		if (n >= 20 && n <= 22)
		{
			return Color.FromArgb(0, 130, 0);
		}
		switch (n)
		{
		case 1:
		case 3:
		case 14:
		case 16:
		case 23:
		case 24:
		case 25:
			return Color.FromArgb(105, 105, 45);
		case 8:
		case 9:
		case 10:
		case 11:
			return Color.Gray;
		case 26:
		case 27:
		case 28:
			return Color.FromArgb(0, 80, 85);
		default:
			return Color.FromArgb(0, 0, 75);
		}
	}

	public static Color HeaderInk(int n)
	{
		if ((n < 20 || n > 22) && n != 3 && n != 14 && n != 16 && n != 8 && n != 9 && n != 10)
		{
			return Color.White;
		}
		return Color.Black;
	}
}
