namespace NLtoSQL
{
	internal static class Matcher
	{
		public static (List<Span> Spans, List<string> Leftover) Match(
			List<string> tokens, DateTime today, Lexicon lexicon)
		{
			var claimed = new bool[tokens.Count];
			var spans = new List<Span>();

			DateStage.Claim(tokens, claimed, spans, today);
			ClaimDictionary(tokens, claimed, spans, lexicon);
			NumberStage.Claim(tokens, claimed, spans);

			var leftover = tokens
				.Where((token, i) => !claimed[i] && !lexicon.Stopwords.Contains(token))
				.ToList();

			return (spans.OrderBy(s => s.Start).ToList(), leftover);
		}

		private static void ClaimDictionary(
			List<string> tokens, bool[] claimed, List<Span> spans, Lexicon lexicon)
		{
			for (int i = 0; i < tokens.Count; i++)
			{
				if (claimed[i]) continue;

				for (int n = Math.Min(lexicon.MaxPhraseLength, tokens.Count - i); n >= 1; n--)
				{
					if (Claims.AnyIn(claimed, i, n)) continue;

					var phrase = string.Join(" ", tokens.Skip(i).Take(n));
					if (!lexicon.Entries.TryGetValue(phrase, out var terms)) continue;

					spans.AddRange(terms.Select(t => new Span(i, n, t, phrase)));
					Claims.Mark(claimed, i, n);
					i += n - 1;
					break;
				}
			}
		}
	}
}