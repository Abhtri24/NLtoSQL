
using Microsoft.Data.SqlClient;
using System.Data;

namespace NLtoSQL;

internal sealed class SqlExecutor
{
	private readonly string _connectionString;

	public SqlExecutor(string connectionString)
	{
		_connectionString = connectionString;
	}

	public DataTable Execute(SqlQuery query)
	{
		using var connection = new SqlConnection(_connectionString);
		using var command = connection.CreateCommand();

		command.CommandTimeout = 30;
		command.CommandText = query.Text;

		foreach (var parameter in query.Parameters)
		{
			command.Parameters.AddWithValue(
				"@" + parameter.Key,
				parameter.Value ?? DBNull.Value);
		}

		connection.Open();

		using var reader = command.ExecuteReader();

		var table = new DataTable();
		table.Load(reader);

		return table;
	}
}
