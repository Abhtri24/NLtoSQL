using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Text;

namespace NLtoSQL
{
	internal static class DateStage
	{
		private static readonly Dictionary<string, int> Months = Enumerable.Range(1, 12).SelectMany(m => new[]
		{
			(DateTimeFormatInfo.InvariantInfo.GetMonthName(m).ToLowerInvariant(), m),
			(DateTimeFormatInfo.InvariantInfo.GetAbbreviatedMonthName(m).ToLowerInvariant(), m)
		})
		.Distinct()
		.ToDictionary(x => x.Item1, x => x.Item2);

		public static void Claim(List<string> tokens, bool[] claimed, List<Span> spans, DateTime today)
		{
			// highlight date phrases, and marking them so they can't be highlighted twice.
			for (int i = 0; i < tokens.Count; i++)
			{
				if (claimed[i]) continue;

				var range = ParseAt(tokens, i, today, out int length);
				if (range is null || Claims.AnyIn(claimed, i, length)) continue;

				var source = string.Join(" ", tokens.Skip(i).Take(length));  // converting ["Oct", "27", "2026"] into "Oct 27 2026"

				spans.Add(new Span(i, length, new Term("period", Kind.Date), source, range));
				Claims.Mark(claimed, i, length); // It stamps a "USED" sign on the words we just processed so the computer doesn't try to read them again.
				i += length - 1;
			}
		}

		private static DateRange? ParseAt(List<string> t, int i, DateTime today, out int length)
		{
			string Word(int k) => i + k < t.Count ? t[i + k] : "";
			length = 1;
			var first = t[i];

			if (first == "today")
				return new(today.Date, today.Date.AddDays(1));
			if (first == "yesterday")
				return new(today.Date.AddDays(-1), today.Date);

			// "last 10 days", "past 3 months": rolling window that ends today (today included)
			if ((first == "last" || first == "past")
				&& int.TryParse(Word(1), out int n)
				&& Singular(Word(2)) is string unit)
			{
				length = 3;
				var end = today.Date.AddDays(1);
				var start = unit switch
				{
					"day" => end.AddDays(-n),
					"week" => end.AddDays(-7 * n),
					"month" => end.AddMonths(-n),
					_ => end.AddYears(-n)
				};
				return new(start, end);


			}

			// "last month", "this year": whole calendar unit
			if ((first is "last" or "this") && (Word(1) is "month" or "year"))
			{
				length = 2;
				var offset = first == "last" ? -1 : 0;
				var start = Word(1) == "month"
					? new DateTime(today.Year, today.Month, 1).AddMonths(offset)
					: new DateTime(today.Year, 1, 1).AddYears(offset);
				return new(start, Word(1) == "month" ? start.AddMonths(1) : start.AddYears(1));
			}

			// "oct", "oct 2026": a month, with or without a year
			if (Months.TryGetValue(first, out int month))
			{
				bool hasYear = IsYear(Word(1));
				int year = hasYear ? int.Parse(Word(1))
								   : (month > today.Month ? today.Year - 1 : today.Year);
				length = hasYear ? 2 : 1;
				var start = new DateTime(year, month, 1);
				return new(start, start.AddMonths(1));
			}

			return null;

		}
		private static string? Singular(string word)
		{
			var w = word.TrimEnd('s');
			return w is "day" or "week" or "month" or "year" ? w : null;
		}


		private static bool IsYear(string s) =>
			s.Length == 4 && int.TryParse(s, out int y) && y is >= 1990 and <= 2100;


	}
}
