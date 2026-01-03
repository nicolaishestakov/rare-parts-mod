using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RareParts.Extensions;

public static class StringExtensions
{
    extension(string s)
    {
        public bool ContainsAny(IEnumerable<string> targets) => targets.Any(s.Contains);
        
        public int? GetYear()
        {
            const string pattern = @"\((\d{4})\)\s*";
            var match = Regex.Match(s, pattern);

            if (match.Success)
            {
                return int.Parse(match.Groups[1].Value);
            }

            return null;
        }
    }
}