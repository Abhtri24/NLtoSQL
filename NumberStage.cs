using System.Globalization;

namespace NLtoSQL
{
	internal static class NumberStage
	{
		public static void Claim(List<string> tokens, bool[] claimed, List<Span> spans)
		{
			for (int i = 0; i < tokens.Count; i++)
			{
				if (claimed[i] ||
					!decimal.TryParse(tokens[i], NumberStyles.Number, CultureInfo.InvariantCulture, out _))
					continue;

				spans.Add(new Span(i, 1, new Term(tokens[i], Kind.Number), tokens[i]));
				claimed[i] = true;
			}
		}
	}
}