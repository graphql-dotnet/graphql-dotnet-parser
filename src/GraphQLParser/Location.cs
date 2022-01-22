namespace GraphQLParser;

/// <summary>
/// Provides the ability to decode a linear character position into a line and column number.
/// </summary>
public readonly struct Location
{
    /// <summary>
    /// Creates location from a given sequence of characters and a linear character position.
    /// </summary>
    /// <param name="source">Input data as a sequence of characters.</param>
    /// <param name="position">Linear character position in the <paramref name="source"/>.</param>
    public Location(ROM source, int position)
    {
        // handle index overflow (EOF)
        int overflowDelta = 0;
        if (position >= source.Length)
        {
            overflowDelta = position - source.Length + 1; // equals 1 in case of EOF
            position = source.Length - 1;
        }

        Line = 1;
        Column = 0;

        var span = source.Span;

        for (int i = 0; i <= position; ++i)
        {
            // each case should either
            // 1) increment Column by 1
            // or
            // 2) increment Line by 1 and reset Column to 0
            switch (span[i])
            {
                case '\n':
                    if (i == position) // \n at the end
                    {
                        ++Column;
                    }
                    else
                    {
                        ++Line;
                        Column = 0;
                    }
                    break;

                case '\r':
                    if (i == position) // \r at the end
                    {
                        ++Column;
                    }
                    else
                    {
                        char next = span[i + 1];
                        if (next == '\n') // \r\n at the end
                        {
                            ++Column;
                        }
                        else
                        {
                            ++Line;
                            Column = 0;
                        }
                    }
                    break;

                default:
                    ++Column;
                    break;
            }
        }

        Column += overflowDelta;
    }

    /// <summary>
    /// The column number on which the character is located.
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// The line number on which the character is located.
    /// </summary>
    public int Line { get; }
}
