using System;
using System.Linq;
using System.Windows.Forms;

namespace EdrLocal;

public sealed class ClockMask
{
	private readonly TextBox box;

	private bool changing;

	private static readonly int[] positions = new int[4] { 0, 1, 3, 4 };

	public static string Value(string value)
	{
		value = (value ?? "").Trim();
		switch (value)
		{
		default:
			return value;
		case "__:__":
		case "---":
		case "":
			return "";
		}
	}

	private static string Mask(string value)
	{
		value = Value(value);
		if (value.Length == 0)
		{
			return "__:__";
		}
		if (value.All(char.IsDigit) && value.Length >= 3 && value.Length <= 4)
		{
			value = value.PadLeft(4, '0');
			return value.Substring(0, 2) + ":" + value.Substring(2);
		}
		return value;
	}

	public ClockMask(TextBox control)
	{
		box = control;
		box.KeyPress += KeyPress;
		box.KeyDown += KeyDown;
		TextBox textBox = box;
		EventHandler value = (object s, EventArgs e) =>
		{
			if (!changing)
			{
				string text = Mask(box.Text);
				if (text != box.Text)
				{
					int selectionStart = box.SelectionStart;
					Set(text, selectionStart);
				}
			}
		};
		textBox.TextChanged += value;
	}

	public void Reset(string value)
	{
		Set(Mask(value), 0);
	}

	private void Set(string value, int cursor)
	{
		changing = true;
		box.Text = value;
		box.SelectionStart = Math.Max(0, Math.Min(value.Length, cursor));
		box.SelectionLength = 0;
		changing = false;
	}

	private void KeyPress(object sender, KeyPressEventArgs e)
	{
		if (char.IsControl(e.KeyChar))
		{
			return;
		}
		e.Handled = true;
		if (e.KeyChar == ':')
		{
			box.SelectionStart = 3;
		}
		else
		{
			if (e.KeyChar < '0' || e.KeyChar > '9')
			{
				return;
			}
			string text = box.Text;
			if (text.Length != 5 || text[2] != ':')
			{
				text = "__:__";
			}
			char[] array = text.ToCharArray();
			int start = box.SelectionStart;
			if (box.SelectionLength > 0)
			{
				int[] array2 = positions;
				foreach (int num in array2)
				{
					if (num >= start && num < start + box.SelectionLength)
					{
						array[num] = '_';
					}
				}
			}
			int num2 = positions.Where((int p) => p >= start).DefaultIfEmpty(-1).First();
			if (num2 >= 0)
			{
				array[num2] = e.KeyChar;
				Set(new string(array), (num2 == 1) ? 3 : (num2 + 1));
			}
		}
	}

	private void KeyDown(object sender, KeyEventArgs e)
	{
		if (e.Control || (e.KeyCode != Keys.Back && e.KeyCode != Keys.Delete))
		{
			return;
		}
		e.SuppressKeyPress = true;
		string text = box.Text;
		if (text.Length != 5 || text[2] != ':')
		{
			Set("__:__", 0);
			return;
		}
		char[] array = text.ToCharArray();
		int start = box.SelectionStart;
		if (box.SelectionLength > 0)
		{
			int[] array2 = positions;
			foreach (int num in array2)
			{
				if (num >= start && num < start + box.SelectionLength)
				{
					array[num] = '_';
				}
			}
			Set(new string(array), start);
		}
		else
		{
			int num2 = ((e.KeyCode == Keys.Back) ? positions.Where((int p) => p < start).DefaultIfEmpty(-1).Last() : positions.Where((int p) => p >= start).DefaultIfEmpty(-1).First());
			if (num2 >= 0)
			{
				array[num2] = '_';
				Set(new string(array), num2);
			}
		}
	}
}
