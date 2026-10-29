using System.Text.Json;

namespace NLtoSQL
{
	internal static class LexiconLoader
	{
		private class LexiconFile
		{
			public Dictionary<string, List<string>> Reports { get; set; } = new();
			public Dictionary<string, List<string>> Columns { get; set; } = new();
			public Dictionary<string, List<string>> Operators { get; set; } = new();
			public List<string> Stopwords { get; set; } = new();
		}

		private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

		public static Lexicon Load(string path)
		{
			var file = JsonSerializer.Deserialize<LexiconFile>(File.ReadAllText(path), Options)
				?? throw new InvalidOperationException($"{path} is empty or invalid.");

			var entries = new Dictionary<string, List<Term>>();
			AddGroup(entries, file.Reports, Kind.Report);
			AddGroup(entries, file.Columns, Kind.Column);
			AddGroup(entries, file.Operators, Kind.Op);

			if (entries.Count == 0)
				throw new InvalidOperationException("The lexicon has no words.");

			var stopwords = file.Stopwords.SelectMany(Tokenizer.Tokenize).ToHashSet();
			return new Lexicon(entries, stopwords);
		}

		private static void AddGroup(
			Dictionary<string, List<Term>> entries, Dictionary<string, List<string>> group, Kind kind)
		{
			foreach (var (canonical, words) in group)
			{
				var term = new Term(canonical, kind);

				foreach (var word in words)
				{
					var key = string.Join(" ", Tokenizer.Tokenize(word));
					if (key.Length == 0)
						throw new InvalidOperationException($"'{word}' under '{canonical}' has no usable tokens.");

					if (!entries.TryGetValue(key, out var terms))
						entries[key] = terms = new List<Term>();
					terms.Add(term);
				}
			}
		}
	}
}