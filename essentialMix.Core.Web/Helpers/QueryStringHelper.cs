namespace essentialMix.Core.Web.Helpers;

public static class QueryStringHelper
{
	public static string SafeHash(string hash)
	{
		if (string.IsNullOrEmpty(hash)) return hash;
		hash = hash.Replace('+', '-');
		return hash.Replace('/', '_');
	}

	public static string UnsafeHash(string hash)
	{
		if (string.IsNullOrEmpty(hash)) return hash;
		hash = hash.Replace('-', '+');
		return hash.Replace('_', '/');
	}
}