using System.Globalization;
using NLtoSQL;

string baseDir = AppDomain.CurrentDomain.BaseDirectory;
string jsonPath = Path.Combine(baseDir, "Config", "lexicon.json");
var lexicon = LexiconLoader.Load(jsonPath);
var mapping = MappingLoader.Load(Path.Combine(baseDir, "Config", "mapping.json"));
MappingLoader.Validate(mapping, lexicon);
var today = DateTime.Today;

var samples = new[]
{
	"give me a bourdreau for oct 2026 for acme re  where premium above 3000",
	"statement of account for last 10 days",
	"premium>=23000",
	"bordereau for last month",
	"report for policies of last year  ",
	"soa where premium above 3000 last month"

};


foreach (var s in samples)
{
	var (spans, leftover) = Matcher.Match(Tokenizer.Tokenize(s), today, lexicon);

	Console.WriteLine(s);
	foreach (var span in spans)
	{
		var detail = span.Range is { } r
			? $"  {Day(r.From)} .. {Day(r.ToExclusive.AddDays(-1))}"
			: "";
		Console.WriteLine($"  [{span.Start}..{span.Start + span.Length - 1}] \"{span.Source}\" -> {span.Term.Kind}:{span.Term.Canonical}{detail}");
	}
	Console.WriteLine($"  UNEXPLAINED: {(leftover.Count == 0 ? "none" : string.Join(", ", leftover))}");

	if (leftover.Count > 0)
	{
		Console.WriteLine("  NOT RUN: unrecognised words");
		continue;
	}

	try
	{
		var query = SqlBuilder.Build(spans, mapping);
		Console.WriteLine("  " + query.Text.Replace("\n", "\n  "));
		foreach (var (name, value) in query.Parameters)
			Console.WriteLine($"  @{name} = {value}");
	}
	catch (QueryException ex)
	{
		Console.WriteLine("  NOT RUN: " + ex.Message);
	}
}

static string Day(DateTime d) => d.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);