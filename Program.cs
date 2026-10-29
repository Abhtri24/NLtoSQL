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

Console.WriteLine(
	$"Loaded {values.Count} database values.");

var lexicon = LexiconLoader.Load(
	Path.Combine(baseDir, "Config", "lexicon.json"),
	values);

MappingLoader.Validate(mapping, lexicon);

var today = DateTime.Today;

string regressionPath = Path.Combine(
	baseDir,
	"Tests",
	"regression.json");

RegressionRunner.Run(
	regressionPath,
	mapping,
	lexicon,
	today);