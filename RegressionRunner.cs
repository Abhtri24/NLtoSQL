
using System.Globalization;
using System.Text.Json;

namespace NLtoSQL;

internal static class RegressionRunner
{
	public static void Run(
		string path,
		Mapping mapping,
		Lexicon lexicon,
		DateTime today)
	{
		if (!File.Exists(path))
		{
			Console.WriteLine($"Regression file not found: {path}");
			return;
		}

		var json = File.ReadAllText(path);

		var tests = JsonSerializer.Deserialize<List<RegressionCase>>(
			json,
			new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			});

		if (tests is null || tests.Count == 0)
		{
			Console.WriteLine("No regression tests found.");
			return;
		}

		int passed = 0;
		int failed = 0;

		Console.WriteLine("NLtoSQL Regression Tests");
		Console.WriteLine(new string('=', 60));

		foreach (var test in tests)
		{
			var failures = new List<string>();

			try
			{
				var (spans, leftover) = Matcher.Match(
					Tokenizer.Tokenize(test.Input),
					today,
					lexicon);

				// An expected failure must not produce a valid query.
				if (!test.ShouldSucceed)
				{
					if (leftover.Count > 0)
					{
						passed++;
						PrintResult(test.Name, true,
							$"Rejected unrecognized words: {string.Join(", ", leftover)}");
						continue;
					}

					try
					{
						var unexpectedQuery = SqlBuilder.Build(spans, mapping);

						if (string.IsNullOrWhiteSpace(test.ExpectedReport))
						{
							failed++;
							PrintResult(test.Name, false,
								"The input unexpectedly generated a query.");
							continue;
						}

						// A negative test with an expected report is not
						// automatically a pass just because SQL was generated.
						failed++;
						PrintResult(test.Name, false,
							"Expected the input to be rejected, but a query was generated.");
						continue;
					}
					catch (QueryException)
					{
						passed++;
						PrintResult(test.Name, true,
							"Rejected by the query builder as expected.");
						continue;
					}
				}

				if (leftover.Count > 0)
				{
					failures.Add(
						$"Unrecognized words: {string.Join(", ", leftover)}");
				}

				if (!string.IsNullOrWhiteSpace(test.ExpectedReport))
				{
					bool reportFound = spans.Any(s =>
						s.Term.Kind == Kind.Report &&
						string.Equals(
							s.Term.Canonical,
							test.ExpectedReport,
							StringComparison.OrdinalIgnoreCase));

					if (!reportFound)
					{
						failures.Add(
							$"Expected report '{test.ExpectedReport}' was not recognized.");
					}
				}

				if (failures.Count > 0)
				{
					failed++;
					PrintResult(test.Name, false, string.Join(" | ", failures));
					continue;
				}

				var query = SqlBuilder.Build(spans, mapping);

				foreach (var filter in test.ExpectedFilters)
				{
					if (!query.Text.Contains(
						filter,
						StringComparison.OrdinalIgnoreCase))
					{
						failures.Add($"Missing SQL fragment: {filter}");
					}
				}

				foreach (var expected in test.ExpectedParameters.Keys)
				{
					bool found = query.Parameters.Values.Any(value =>
						string.Equals(
							Convert.ToString(value, CultureInfo.InvariantCulture),
							expected,
							StringComparison.OrdinalIgnoreCase));

					if (!found)
					{
						failures.Add(
							$"Expected parameter value '{expected}' was not found.");
					}
				}

				if (failures.Count == 0)
				{
					passed++;
					PrintResult(test.Name, true, "All expectations matched.");
				}
				else
				{
					failed++;
					PrintResult(test.Name, false, string.Join(" | ", failures));
				}
			}
			catch (QueryException ex)
			{
				failed++;
				PrintResult(test.Name, false, $"Unexpected query error: {ex.Message}");
			}
			catch (Exception ex)
			{
				failed++;
				PrintResult(test.Name, false,
					$"Unexpected error: {ex.GetType().Name}: {ex.Message}");
			}
		}

		Console.WriteLine();
		Console.WriteLine(new string('=', 60));
		Console.WriteLine($"Results: {passed} passed, {failed} failed, {tests.Count} total");
		Console.WriteLine(failed == 0
			? "All regression tests passed."
			: "Some regression tests failed.");
	}

	private static void PrintResult(
		string name,
		bool passed,
		string detail)
	{
		Console.WriteLine(
			$"{(passed ? "PASS" : "FAIL")} | {name}");

		if (!passed)
		{
			Console.WriteLine($"       {detail}");
		}
	}

	private sealed class RegressionCase
	{
		public string Name { get; set; } = "";
		public string Input { get; set; } = "";
		public string? ExpectedReport { get; set; }
		public List<string> ExpectedFilters { get; set; } = new();
		public Dictionary<string, bool> ExpectedParameters { get; set; } = new();
		public bool ShouldSucceed { get; set; } = true;
	}
}
