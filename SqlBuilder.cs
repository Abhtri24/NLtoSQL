
using System.Globalization;
using System.Text.RegularExpressions;

namespace NLtoSQL;

internal class QueryException : Exception
{
	public QueryException(string message) : base(message) { }
}

internal record SqlQuery(
	string Text,
	Dictionary<string, object> Parameters);

internal static class SqlBuilder
{
	private static readonly HashSet<string> AllowedOps =
		new(StringComparer.Ordinal)
		{
			">", ">=", "<", "<="
		};

	private static readonly Regex SafeIdentifier = new(
		@"^[A-Za-z_][A-Za-z0-9_]*$",
		RegexOptions.Compiled);

	private sealed record ValueMatch(
		int Start,
		int Length,
		string Column,
		string Value,
		string Source);

	public static SqlQuery Build(List<Span> spans, Mapping map)
	{
		var report = PickReport(spans, map);

		var where = new List<string>();
		var args = new Dictionary<string, object>();

		AddDateFilter(spans, report, where, args);
		AddValueFilters(spans, map, where, args);
		AddComparisons(spans, map, where, args);

		var select = report.Select
			.Select(QuoteIdentifier)
			.Concat(report.Sum.Select(c =>
			{
				if (!map.Columns.TryGetValue(c, out var definition))
				{
					throw new QueryException(
						$"Measure '{c}' has no column mapping.");
				}

				return $"SUM({QuoteIdentifier(definition.Sql)}) " +
					   $"AS {QuoteIdentifier(c)}";
			}))
			.ToList();

		if (select.Count == 0)
			throw new QueryException("The report has no selected fields.");

		var sql =
			$"SELECT {string.Join(", ", select)}\n" +
			$"FROM {QuoteTable(map.Table)}";

		if (where.Count > 0)
			sql += "\nWHERE " + string.Join("\n  AND ", where);

		if (report.Sum.Count > 0)
		{
			sql += "\nGROUP BY " +
				   string.Join(", ", report.Select.Select(QuoteIdentifier));
		}

		return new SqlQuery(sql, args);
	}

	private static ReportDef PickReport(
		List<Span> spans,
		Mapping map)
	{
		var ids = spans
			.Where(s => s.Term.Kind == Kind.Report)
			.Select(s => s.Term.Canonical)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		if (ids.Count == 0)
		{
			throw new QueryException(
				"Name a report, e.g. SOA, bordereau or claims.");
		}

		if (ids.Count > 1)
			throw new QueryException("Ask for one report at a time.");

		if (!map.Reports.TryGetValue(ids[0], out var report))
		{
			throw new QueryException(
				$"Report '{ids[0]}' has no mapping.");
		}

		return report;
	}

	private static void AddDateFilter(
		List<Span> spans,
		ReportDef report,
		List<string> where,
		Dictionary<string, object> args)
	{
		var ranges = spans
			.Where(s => s.Range is not null)
			.ToList();

		if (ranges.Count > 1)
			throw new QueryException("Give one date range.");

		if (ranges.Count == 0)
			return;

		var range = ranges[0].Range!;

		where.Add(
			$"{QuoteIdentifier(report.DateColumn)} >= @dateFrom " +
			$"AND {QuoteIdentifier(report.DateColumn)} < @dateTo");

		args["dateFrom"] = range.From;
		args["dateTo"] = range.ToExclusive;
	}

	private static void AddValueFilters(
		List<Span> spans,
		Mapping map,
		List<string> where,
		Dictionary<string, object> args)
	{
		var matches = ResolveValueOccurrences(spans);

		// Preserve the order in which filters appear in the request.
		foreach (var columnGroup in matches
			.GroupBy(m => m.Column, StringComparer.OrdinalIgnoreCase)
			.OrderBy(g => g.Min(m => m.Start)))
		{
			var column = columnGroup.First().Column;

			if (!map.ValueColumns.Contains(
				column, StringComparer.OrdinalIgnoreCase))
			{
				throw new QueryException(
					$"Value column '{column}' is not configured.");
			}

			var values = columnGroup
				.OrderBy(m => m.Start)
				.Select(m => m.Value)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

			if (values.Count == 0)
				continue;

			var parameters = new List<string>();

			foreach (var value in values)
			{
				var name = $"v{args.Count}";
				args[name] = value;
				parameters.Add($"@{name}");
			}

			var sqlColumn = QuoteIdentifier(column);

			if (parameters.Count == 1)
			{
				where.Add($"{sqlColumn} = {parameters[0]}");
			}
			else
			{
				where.Add(
					$"{sqlColumn} IN ({string.Join(", ", parameters)})");
			}
		}
	}

	private static List<ValueMatch> ResolveValueOccurrences(
		List<Span> spans)
	{
		var result = new List<ValueMatch>();

		// Terms produced by one phrase share the same token range.
		foreach (var occurrence in spans.GroupBy(
			s => (s.Start, s.Length)))
		{
			var valueTerms = occurrence
				.Where(s => s.Term.Kind == Kind.Value)
				.ToList();

			if (valueTerms.Count == 0)
				continue;

			// A phrase that is both a value and another kind of term
			// is ambiguous. Do not silently guess its meaning.
			if (occurrence.Any(s => s.Term.Kind != Kind.Value))
			{
				throw new QueryException(
					$"'{valueTerms[0].Source}' has an ambiguous meaning.");
			}

			var columns = valueTerms
				.Select(s => s.Term.Column)
				.Where(c => !string.IsNullOrWhiteSpace(c))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

			if (columns.Count == 0)
			{
				throw new QueryException(
					$"No database column is associated with " +
					$"'{valueTerms[0].Source}'.");
			}

			if (columns.Count > 1)
			{
				throw new QueryException(
					$"'{valueTerms[0].Source}' matches multiple columns: " +
					string.Join(", ", columns) +
					". Please specify which field you mean.");
			}

			var column = columns[0]!;

			var valuesForColumn = valueTerms
				.Where(s => string.Equals(
					s.Term.Column,
					column,
					StringComparison.OrdinalIgnoreCase))
				.Select(s => s.Term.Canonical)
				.Distinct(StringComparer.OrdinalIgnoreCase);

			foreach (var value in valuesForColumn)
			{
				result.Add(new ValueMatch(
					occurrence.Key.Start,
					occurrence.Key.Length,
					column,
					value,
					valueTerms[0].Source));
			}
		}

		return result;
	}

	private static void AddComparisons(
		List<Span> spans,
		Mapping map,
		List<string> where,
		Dictionary<string, object> args)
	{
		var parts = spans
			.Where(s => s.Term.Kind is
				Kind.Column or Kind.Op or Kind.Number)
			.ToList();

		int i = 0;

		while (i < parts.Count)
		{
			if (i + 2 < parts.Count
				&& parts[i].Term.Kind == Kind.Column
				&& parts[i + 1].Term.Kind == Kind.Op
				&& parts[i + 2].Term.Kind == Kind.Number)
			{
				AddComparison(
					parts[i],
					parts[i + 1],
					parts[i + 2],
					map,
					where,
					args);

				i += 3;
			}
			else if (parts[i].Term.Kind == Kind.Column)
			{
				// A column mentioned without a condition is not a filter.
				i++;
			}
			else
			{
				throw new QueryException(
					$"Couldn't use '{parts[i].Source}'; " +
					"expected 'column operator number'.");
			}
		}
	}

	private static void AddComparison(
		Span column,
		Span op,
		Span number,
		Mapping map,
		List<string> where,
		Dictionary<string, object> args)
	{
		if (!AllowedOps.Contains(op.Term.Canonical))
		{
			throw new QueryException(
				$"Unsupported operator '{op.Source}'.");
		}

		if (!map.Columns.TryGetValue(
			column.Term.Canonical, out var definition))
		{
			throw new QueryException(
				$"Column '{column.Term.Canonical}' has no mapping.");
		}

		if (!decimal.TryParse(
			number.Term.Canonical,
			NumberStyles.Number,
			CultureInfo.InvariantCulture,
			out var value))
		{
			throw new QueryException(
				$"'{number.Source}' is not a valid number.");
		}

		var name = $"p{args.Count}";
		where.Add(
			$"{QuoteIdentifier(definition.Sql)} " +
			$"{op.Term.Canonical} @{name}");

		args[name] = value;
	}

	private static string QuoteIdentifier(string identifier)
	{
		if (!SafeIdentifier.IsMatch(identifier))
		{
			throw new QueryException(
				$"Invalid SQL identifier '{identifier}'.");
		}

		return $"[{identifier}]";
	}

	private static string QuoteTable(string table)
	{
		var parts = table.Split('.');

		if (parts.Length == 0 ||
			parts.Any(p => !SafeIdentifier.IsMatch(p)))
		{
			throw new QueryException(
				$"Invalid SQL table name '{table}'.");
		}

		return string.Join(".", parts.Select(QuoteIdentifier));
	}
}
