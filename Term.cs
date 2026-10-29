using System;
using System.Collections.Generic;
using System.Text;

namespace NLtoSQL
{
	internal enum Kind { Report, Column, Op, Date, Number, Value }
	internal record Term(string Canonical, Kind Kind, string? Column = null);


}
