using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace NLtoSQL;

internal sealed record DatabaseValue(string Column, string Value);

internal static class ValueSource
{
	private static readonly Regex SafeIdentifier =
		new(@"^[A-Za-z_][A-Za-z0-9_]*$",
			RegexOptions.Compiled);

	public static List<DatabaseValue> Load(
		string connectionString,
		string table,
		IEnumerable<string> columns)
	{
		var tableName = QuoteTable(table);
		var result = new List<DatabaseValue>();

		using var connection = new SqlConnection(connectionString);
		connection.Open();

		foreach (var column in columns.Distinct(
					 StringComparer.OrdinalIgnoreCase))
		{
			if (!SafeIdentifier.IsMatch(column))
				throw new InvalidOperationException(
					$"Invalid configured column: {column}");

			var identifier = $"[{column}]";

			var sql = $"""
                SELECT DISTINCT CONVERT(nvarchar(4000), {identifier})
                FROM {tableName}
                WHERE {identifier} IS NOT NULL
                  AND LTRIM(RTRIM(
                      CONVERT(nvarchar(4000), {identifier})
                  )) <> ''
                """;

			using var command = new SqlCommand(sql, connection);
			command.CommandTimeout = 10;

			using var reader = command.ExecuteReader();

			while (reader.Read())
			{
				var value = reader.GetString(0).Trim();

				if (value.Length > 0)
					result.Add(new DatabaseValue(column, value));
			}
		}

		return result
			.DistinctBy(v => (v.Column, v.Value))
			.ToList();
	}

	private static string QuoteTable(string table)
	{
		var parts = table.Split('.');

		if (parts.Length == 1)
			parts = ["dbo", parts[0]];

		if (parts.Length != 2 ||
			parts.Any(p => !SafeIdentifier.IsMatch(p)))
		{
			throw new InvalidOperationException(
				$"Invalid configured table: {table}");
		}

		return string.Join(".", parts.Select(p => $"[{p}]"));
	}
}