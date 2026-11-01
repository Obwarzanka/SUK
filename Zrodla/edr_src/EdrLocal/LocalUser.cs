using System;
using System.Security.Cryptography;

namespace EdrLocal;

public class LocalUser
{
	public string Id { get; set; }

	public string Name { get; set; }

	public string Role { get; set; }

	public string Salt { get; set; }

	public string Hash { get; set; }

	public void SetPassword(string password)
	{
		if (password.Length < 4)
		{
			throw new Exception("Hasło musi mieć co najmniej 4 znaki.");
		}
		byte[] array = new byte[16];
		using (RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create())
		{
			randomNumberGenerator.GetBytes(array);
		}
		Salt = Convert.ToBase64String(array);
		using Rfc2898DeriveBytes rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, array, 60000);
		Hash = Convert.ToBase64String(rfc2898DeriveBytes.GetBytes(32));
	}

	public bool Verify(string password)
	{
		using Rfc2898DeriveBytes rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, Convert.FromBase64String(Salt), 60000);
		byte[] bytes = rfc2898DeriveBytes.GetBytes(32);
		byte[] array = Convert.FromBase64String(Hash);
		int num = bytes.Length ^ array.Length;
		for (int i = 0; i < bytes.Length && i < array.Length; i++)
		{
			num |= bytes[i] ^ array[i];
		}
		return num == 0;
	}
}
