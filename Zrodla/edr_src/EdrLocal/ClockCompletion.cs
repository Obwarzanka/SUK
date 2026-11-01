using System;
using System.Windows.Forms;

namespace EdrLocal;

internal sealed class ClockCompletion : IDisposable
{
	private readonly TextBox owner;

	private readonly Func<Func<DateTime>> clock;

	private readonly Func<bool> allowed;

	private readonly Action<string> render;

	private readonly Action<DateTime> choose;

	private bool active;

	private bool writing;

	private bool disposed;

	private DateTime offered;

	public bool Visible => active;

	public string SuggestionText
	{
		get
		{
			if (!active)
			{
				return "";
			}
			return offered.ToString("HH:mm");
		}
	}

	public ClockCompletion(TextBox box, Func<Func<DateTime>> source, Func<bool> enabled, Action<string> show, Action<DateTime> accept)
	{
		owner = box;
		clock = source;
		allowed = enabled;
		render = show;
		choose = accept;
		TextBox textBox = owner;
		MouseEventHandler value = (object s, MouseEventArgs e) =>
		{
			if (e.Button == MouseButtons.Left)
			{
				Show();
			}
		};
		textBox.MouseUp += value;
		owner.TextChanged += (object s, EventArgs e) =>
		{
			if (!writing)
			{
				active = false;
			}
		};
		owner.Leave += (object s, EventArgs e) =>
		{
			Hide();
		};
		owner.VisibleChanged += (object s, EventArgs e) =>
		{
			if (!owner.Visible)
			{
				Hide();
			}
		};
		owner.KeyDown += (object s, KeyEventArgs e) =>
		{
			if (e.Modifiers == Keys.None)
			{
				if (e.KeyCode == Keys.Escape && active)
				{
					Hide();
					e.SuppressKeyPress = true;
				}
				else if ((e.KeyCode == Keys.Return || e.KeyCode == Keys.Tab) && Accept())
				{
					e.SuppressKeyPress = true;
				}
			}
		};
	}

	public void Show()
	{
		if (disposed || owner.IsDisposed || !owner.Visible || !owner.Enabled || owner.ReadOnly || !allowed())
		{
			return;
		}
		if (!active)
		{
			Func<DateTime> func = clock();
			if (func == null || ClockMask.Value(owner.Text).Length != 0)
			{
				return;
			}
			offered = func();
			active = true;
			Write(offered.ToString("HH:mm"));
		}
		owner.SelectAll();
	}

	private void Write(string text)
	{
		writing = true;
		try
		{
			render(text);
		}
		finally
		{
			writing = false;
		}
	}

	public bool Accept()
	{
		if (!active || disposed || owner.ReadOnly || !owner.Enabled || !allowed())
		{
			return false;
		}
		active = false;
		choose(offered);
		owner.SelectAll();
		return true;
	}

	public void Hide()
	{
		if (active)
		{
			Write("");
			active = false;
		}
	}

	public void Forget()
	{
		active = false;
	}

	public void Dispose()
	{
		active = false;
		disposed = true;
	}
}
