using System.Globalization;
using System.Text;

namespace ArmaVoice.Server.Game;

/// <summary>
/// Holds the latest game state snapshot, updated each time a state message arrives
/// from the Arma extension.
/// </summary>
public class GameState
{
    public float[] PlayerPos { get; set; } = [0, 0, 0];
    public float PlayerDir { get; set; }
    public List<(string NetId, float[] Pos)> NearbyUnits { get; } = new();

    /// <summary>
    /// Update state from a raw SQF array string.
    /// Expected format: [[x,y,z],dir,[["netId",[x,y,z]], ...]]
    /// </summary>
    public void UpdateFromState(string sqfArrayString)
    {
        try
        {
            var parsed = SqfParser.Parse(sqfArrayString);
            if (parsed is not List<object> root || root.Count < 3)
            {
                Console.WriteLine($"[GameState] Invalid state root (expected 3-element array): {sqfArrayString[..Math.Min(80, sqfArrayString.Length)]}");
                return;
            }

            // Element 0: player position [x, y, z]
            if (root[0] is List<object> posArray && posArray.Count >= 3)
            {
                PlayerPos = [ToFloat(posArray[0]), ToFloat(posArray[1]), ToFloat(posArray[2])];
            }

            // Element 1: player direction (degrees)
            PlayerDir = ToFloat(root[1]);

            // Element 2: nearby units [["netId",[x,y,z]], ...]
            NearbyUnits.Clear();
            if (root[2] is List<object> unitsArray)
            {
                foreach (var item in unitsArray)
                {
                    if (item is List<object> unitEntry && unitEntry.Count >= 2)
                    {
                        var netId = unitEntry[0]?.ToString() ?? "";
                        float[] unitPos = [0, 0, 0];

                        if (unitEntry[1] is List<object> uPos && uPos.Count >= 3)
                        {
                            unitPos = [ToFloat(uPos[0]), ToFloat(uPos[1]), ToFloat(uPos[2])];
                        }

                        if (!string.IsNullOrEmpty(netId))
                        {
                            NearbyUnits.Add((netId, unitPos));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameState] Parse error: {ex.Message}");
        }
    }

    private static float ToFloat(object obj)
    {
        return obj switch
        {
            double d => (float)d,
            float f => f,
            int i => i,
            string s when float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) => v,
            _ => 0f
        };
    }
}

/// <summary>
/// Simple recursive parser for SQF array format.
/// Handles: arrays [...], strings "...", numbers (int/float), booleans (true/false).
/// </summary>
public static class SqfParser
{
    /// <summary>
    /// Parse an SQF value string into a CLR object tree.
    /// Arrays become List&lt;object&gt;, strings become string, numbers become double, booleans become bool.
    /// </summary>
    public static object? Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        int pos = 0;
        var result = ParseValue(input, ref pos);
        return result;
    }

    private static object? ParseValue(string input, ref int pos)
    {
        SkipWhitespace(input, ref pos);

        if (pos >= input.Length)
            return null;

        char c = input[pos];

        if (c == '[')
            return ParseArray(input, ref pos);

        if (c == '"')
            return ParseString(input, ref pos);

        if (c == '-' || char.IsDigit(c))
            return ParseNumber(input, ref pos);

        // Check for boolean keywords
        if (pos + 4 <= input.Length && input[pos..(pos + 4)].Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            pos += 4;
            return true;
        }

        if (pos + 5 <= input.Length && input[pos..(pos + 5)].Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            pos += 5;
            return false;
        }

        // Unknown token - try to skip a word
        var start = pos;
        while (pos < input.Length && !char.IsWhiteSpace(input[pos]) && input[pos] != ',' && input[pos] != ']')
            pos++;

        return input[start..pos];
    }

    private static List<object> ParseArray(string input, ref int pos)
    {
        var list = new List<object>();

        pos++; // skip '['

        SkipWhitespace(input, ref pos);

        if (pos < input.Length && input[pos] == ']')
        {
            pos++; // empty array
            return list;
        }

        while (pos < input.Length)
        {
            var value = ParseValue(input, ref pos);
            if (value != null)
            {
                list.Add(value);
            }

            SkipWhitespace(input, ref pos);

            if (pos >= input.Length)
                break;

            if (input[pos] == ',')
            {
                pos++; // skip comma
                continue;
            }

            if (input[pos] == ']')
            {
                pos++; // end of array
                break;
            }

            // Unexpected character, skip it
            pos++;
        }

        return list;
    }

    private static string ParseString(string input, ref int pos)
    {
        var sb = new StringBuilder();
        pos++; // skip opening '"'

        while (pos < input.Length)
        {
            char c = input[pos];

            if (c == '"')
            {
                // Check for escaped quote (SQF uses "" for literal ")
                if (pos + 1 < input.Length && input[pos + 1] == '"')
                {
                    sb.Append('"');
                    pos += 2;
                    continue;
                }

                // End of string
                pos++;
                break;
            }

            sb.Append(c);
            pos++;
        }

        return sb.ToString();
    }

    private static double ParseNumber(string input, ref int pos)
    {
        var start = pos;

        if (pos < input.Length && input[pos] == '-')
            pos++;

        while (pos < input.Length && char.IsDigit(input[pos]))
            pos++;

        // Decimal part
        if (pos < input.Length && input[pos] == '.')
        {
            pos++;
            while (pos < input.Length && char.IsDigit(input[pos]))
                pos++;
        }

        // Scientific notation
        if (pos < input.Length && (input[pos] == 'e' || input[pos] == 'E'))
        {
            pos++;
            if (pos < input.Length && (input[pos] == '+' || input[pos] == '-'))
                pos++;
            while (pos < input.Length && char.IsDigit(input[pos]))
                pos++;
        }

        var numberStr = input[start..pos];

        if (double.TryParse(numberStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return value;

        return 0.0;
    }

    private static void SkipWhitespace(string input, ref int pos)
    {
        while (pos < input.Length && char.IsWhiteSpace(input[pos]))
            pos++;
    }
}
