namespace NLtoSQL
{
	internal record Lexicon(Dictionary<string, List<Term>> Entries, HashSet<string> Stopwords)
	{

		public int MaxPhraseLength { get; } = Entries.Keys.Max(k => k.Split(' ').Length);
}
}