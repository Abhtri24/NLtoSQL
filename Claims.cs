using System;
using System.Collections.Generic;
using System.Text;

namespace NLtoSQL
{
    internal static class Claims

	{
        public static bool AnyIn(bool[] claimed , int start , int length)=>
            claimed.Skip(start).Take(length).Any(c=>c);
        public static void Mark(bool[] claimed, int start, int length)
        { for (int k = start; k < start + length; k++) claimed[k] = true;
		}
	}
}
