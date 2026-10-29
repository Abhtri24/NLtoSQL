using System.Text.Json;

namespace NLtoSQL
{
	internal class ColumnDef { public string Sql { get; set; } = ""; }

	internal class ReportDef
	{
		public string DateColumn { get; set; } = "";
		public List<string> Select { get; set; } = new();
		public List<string> Sum { get; set; } = new();

	}

	internal class Mapping
	{
		public string Table { get; set; } = "";
		public Dictionary<string, ColumnDef> Columns { get; set; } = new();
		public Dictionary<string, ReportDef> Reports { get; set; } = new();
		public List<string> ValueColumns { get; set; } = new();

	}

	internal static class MappingLoader
	{
		private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

		public static Mapping Load(string path) =>
			JsonSerializer.Deserialize<Mapping>(File.ReadAllText(path), Options)
				?? throw new InvalidOperationException($"{path} is empty or invalid.");

		// Fails at startup if the lexicon has a word the mapping can't explain.
		public static void Validate(Mapping map, Lexicon lexicon)
		{
			var missing = lexicon.Entries.Values.SelectMany(t => t)
				.Where(t => (t.Kind == Kind.Report && !map.Reports.ContainsKey(t.Canonical))
						 || (t.Kind == Kind.Column && !map.Columns.ContainsKey(t.Canonical)))
				.Select(t => $"{t.Kind}:{t.Canonical}")
				.Distinct()
				.ToList();

			if (missing.Count > 0)
				throw new InvalidOperationException("Lexicon entries without a mapping: " + string.Join(", ", missing));
		}
	}
}