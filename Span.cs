using System;
using System.Collections.Generic;
using System.Text;

namespace NLtoSQL
{
	internal record DateRange(DateTime From, DateTime ToExclusive);

	internal record Span(int Start, int Length, Term Term, string Source, DateRange? Range = null);
    
}
