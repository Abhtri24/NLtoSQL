using System.Globalization;

namespace NLtoSQL
{
	internal class QueryException : Exception
	{
		public QueryException(string message) : base(message) { }
	}

	internal record SqlQuery(string Text, Dictionary<string, object> Parameters);

	internal static class SqlBuilder
	{
		private static readonly HashSet<string> AllowedOps = new() { ">", ">=", "<", "<=" };

		public static SqlQuery Build(List<Span> spans, Mapping map)
		{
			var report = PickReport(spans, map);
			var where = new List<string>();
			var args = new Dictionary<string, object>();

			AddDateFilter(spans, report, where, args);
			AddComparisons(spans, map, where, args);

			var select = report.Select.Concat(report.Sum.Select(c => $"SUM({map.Columns[c].Sql}) AS {c}"));
			var sql = $"SELECT {string.Join(", ", select)}\nFROM {map.Table}";
			if (where.Count > 0) sql += "\nWHERE " + string.Join("\n  AND ", where);
			if (report.Sum.Count > 0) sql += "\nGROUP BY " + string.Join(", ", report.Select);

			return new SqlQuery(sql, args);
		}

		private static ReportDef PickReport(List<Span> spans, Mapping map)
		{
			var ids = spans.Where(s => s.Term.Kind == Kind.Report)
				.Select(s => s.Term.Canonical).Distinct().ToList();

			if (ids.Count == 0) throw new QueryException("Name a report, e.g. SOA, bordereau or claims.");
			if (ids.Count > 1) throw new QueryException("Ask for one report at a time.");
			return map.Reports[ids[0]];
		}

		private static void AddDateFilter(
			List<Span> spans, ReportDef report, List<string> where, Dictionary<string, object> args)
		{
			var ranges = spans.Where(s => s.Range is not null).ToList();
			if (ranges.Count > 1) throw new QueryException("Give one date range.");
			if (ranges.Count == 0) return;

			var range = ranges[0].Range!;
			where.Add($"{report.DateColumn} >= @dateFrom AND {report.DateColumn} < @dateTo");
			args["dateFrom"] = range.From;
			args["dateTo"] = range.ToExclusive;
		}

		// Reads "column operator number" triples left to right.
		private static void AddComparisons(
			List<Span> spans, Mapping map, List<string> where, Dictionary<string, object> args)
		{
			var parts = spans
				.Where(s => s.Term.Kind is Kind.Column or Kind.Op or Kind.Number)
				.ToList();

			int i = 0;
			while (i < parts.Count)
			{
				if (i + 2 < parts.Count
					&& parts[i].Term.Kind == Kind.Column
					&& parts[i + 1].Term.Kind == Kind.Op
					&& parts[i + 2].Term.Kind == Kind.Number)
				{
					AddComparison(parts[i], parts[i + 1], parts[i + 2], map, where, args);
					i += 3;
				}
				else if (parts[i].Term.Kind == Kind.Column)
				{
					i++;   // a column mentioned without a condition: nothing to filter
				}
				else
				{
					throw new QueryException($"Couldn't use '{parts[i].Source}'; expected 'column operator number'.");
				}
			}
		}

		private static void AddComparison(
			Span column, Span op, Span number, Mapping map,
			List<string> where, Dictionary<string, object> args)
		{
			if (!AllowedOps.Contains(op.Term.Canonical))
				throw new QueryException($"Unsupported operator '{op.Source}'.");

			if (!decimal.TryParse(number.Term.Canonical, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
				throw new QueryException($"'{number.Source}' is not a valid number.");

			var name = $"p{args.Count}";
			where.Add($"{map.Columns[column.Term.Canonical].Sql} {op.Term.Canonical} @{name}");
			args[name] = value;
		}
	}
}