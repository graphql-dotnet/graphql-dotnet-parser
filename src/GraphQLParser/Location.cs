using System.Text.RegularExpressions;

namespace GraphQLParser
{
    public readonly struct Location
    {
        private static readonly Regex _lineRegex = new Regex("\r\n|[\n\r]", RegexOptions.ECMAScript);

        public Location(ISource source, int position)
            : this(source.Body, position)
        {
           
        }

        public Location(string source, int position)
        {
            Line = 1;
            Column = position + 1;

            if (position > 0)
            {
                var matches = _lineRegex.Matches(source);
                foreach (Match match in matches)
                {
                    if (match.Index >= position)
                        break;

                    Line++;
                    Column = position + 1 - (match.Index + matches[0].Length);
                }
            }
        }

        public int Column { get; }

        public int Line { get; }
    }
}
