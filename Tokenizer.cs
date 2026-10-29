using System.Text.RegularExpressions;

namespace NLtoSQL
{
	internal static class Tokenizer
	{
		private static readonly Regex TokenPattern =
			new(@"[a-z]+\d*|\d+(\.\d+)?|[<>=]+", RegexOptions.Compiled);

		public static List<string> Tokenize(string text) =>
			TokenPattern.Matches(text.ToLowerInvariant().Replace(",", ""))
						.Select(m => m.Value)
						.ToList();
	}
}