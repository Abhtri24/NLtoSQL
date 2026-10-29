
using NLtoSQL;

string baseDir = AppDomain.CurrentDomain.BaseDirectory;

string connectionString =
	Environment.GetEnvironmentVariable("NLTOSQL_CONN")
	?? throw new InvalidOperationException(
		"Set the NLTOSQL_CONN environment variable.");

var mapping = MappingLoader.Load(
	Path.Combine(baseDir, "Config", "mapping.json"));

var values = ValueSource.Load(
	connectionString,
	mapping.Table,
	mapping.ValueColumns);

Console.WriteLine($"Loaded {values.Count} database values.");

var lexicon = LexiconLoader.Load(
	Path.Combine(baseDir, "Config", "lexicon.json"),
	values);

MappingLoader.Validate(mapping, lexicon);
var executor  = new SqlExecutor(connectionString);
var today = DateTime.Today;

// Keep regression tests available, but don't run them by default.
if (args.Contains("--test", StringComparer.OrdinalIgnoreCase))
{
	string regressionPath = Path.Combine(
		baseDir, "Tests", "regression.json");

	RegressionRunner.Run(regressionPath, mapping, lexicon, today);
	return;
}

Console.WriteLine();
Console.WriteLine("NLtoSQL Interactive Mode");
Console.WriteLine("Type a query to inspect generated SQL.");
Console.WriteLine("Type 'exit' to quit.");
Console.WriteLine(new string('-', 50));

while (true)
{
	Console.Write("\nNLtoSQL> ");

	string? input = Console.ReadLine();

	if (input is null ||
		input.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
	{
		break;
	}

	if (string.IsNullOrWhiteSpace(input))
		continue;

	try
	{
		var tokens = Tokenizer.Tokenize(input);
		var (spans, leftover) = Matcher.Match(tokens, today, lexicon);

		Console.WriteLine("\nMatched terms:");

		if (spans.Count == 0)
		{
			Console.WriteLine("  No terms recognized.");
		}

		foreach (var span in spans)
		{
			string detail =
				$"  {span.Source} -> {span.Term.Kind}: {span.Term.Canonical}";

			if (!string.IsNullOrWhiteSpace(span.Term.Column))
				detail += $" [column: {span.Term.Column}]";

			if (span.Range is not null)
				detail += $" [{span.Range.From:yyyy-MM-dd} to {span.Range.ToExclusive:yyyy-MM-dd})";

			Console.WriteLine(detail);
		}

		if (leftover.Count > 0)
		{
			Console.WriteLine(
				$"\nUnrecognized words: {string.Join(", ", leftover)}");
			Console.WriteLine("Query not generated. Resolve these terms first.");
			continue;
		}

		var query = SqlBuilder.Build(spans, mapping);

		Console.WriteLine("\nGenerated SQL:");
		Console.WriteLine(query.Text);

		Console.WriteLine("\nParameters:");

		if (query.Parameters.Count == 0)
		{
			Console.WriteLine("  None");
		}

		foreach (var parameter in query.Parameters)
		{
			Console.WriteLine($"  @{parameter.Key} = {parameter.Value}");
		}

		var results = executor.Execute(query);

Console.WriteLine("\nResults:");

if (results.Rows.Count == 0)
{
    Console.WriteLine("No matching records found.");
}
else
{
    foreach (System.Data.DataColumn column in results.Columns)
        Console.Write($"{column.ColumnName,-22}");

    Console.WriteLine();

    foreach (System.Data.DataRow row in results.Rows)
    {
        foreach (var value in row.ItemArray)
            Console.Write($"{value,-22}");

        Console.WriteLine();
    }

    Console.WriteLine($"\nReturned {results.Rows.Count} rows.");
}
	}
	catch (QueryException ex)
	{
		Console.WriteLine($"\nQuery rejected: {ex.Message}");
	}
	catch (Exception ex)
	{
		Console.WriteLine(
			$"\nError: {ex.GetType().Name}: {ex.Message}");
	}
}

Console.WriteLine("\nNLtoSQL closed.");
