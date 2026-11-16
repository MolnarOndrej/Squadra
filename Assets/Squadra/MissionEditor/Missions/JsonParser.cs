using System;
using System.Collections.Generic;
using System.Text;

namespace Squadra.Missions
{
    /// <summary>
    /// A JSON reader that takes the standard at its word: no comments, no trailing commas, no bare
    /// words, nothing after the document, and an object may not name the same property twice.
    /// </summary>
    sealed class JsonParser
    {
        const int MaxDepth = 32;

        readonly string text;
        int position;
        int depth;

        public JsonParser(string text)
        {
            this.text = text;
        }

        public JsonValue ParseDocument()
        {
            // A byte order mark that survived decoding is not part of the document.
            if (text.Length > 0 && text[0] == '﻿') position = 1;

            SkipWhitespace();
            if (position >= text.Length) throw Fail("The file is empty. A mission file is one JSON object.");

            JsonValue value = ParseValue();
            SkipWhitespace();
            if (position < text.Length) throw Fail("There is extra text after the end of the mission object.");
            return value;
        }

        JsonValue ParseValue()
        {
            SkipWhitespace();
            if (position >= text.Length) throw Fail("The file ends where a value was expected.");

            char c = text[position];
            if (c == '{') return ParseObject();
            if (c == '[') return ParseArray();
            if (c == '"') return new JsonValue { Kind = JsonKind.String, Text = ParseString() };
            if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();
            if (TryWord("true")) return new JsonValue { Kind = JsonKind.True };
            if (TryWord("false")) return new JsonValue { Kind = JsonKind.False };
            if (TryWord("null")) return new JsonValue { Kind = JsonKind.Null };
            throw Fail("Unexpected character '" + c + "' where a value was expected.");
        }

        JsonValue ParseObject()
        {
            Enter();
            position++;
            var value = new JsonValue { Kind = JsonKind.Object, Members = new List<KeyValuePair<string, JsonValue>>() };
            var names = new HashSet<string>(StringComparer.Ordinal);

            SkipWhitespace();
            if (Peek() == '}')
            {
                position++;
                depth--;
                return value;
            }

            while (true)
            {
                SkipWhitespace();
                if (Peek() != '"') throw Fail("A property name in double quotes was expected here.");

                int nameStart = position;
                string name = ParseString();
                if (!names.Add(name))
                {
                    position = nameStart;
                    throw Fail("The property \"" + name + "\" appears twice in the same object. "
                        + "Remove one so that it is clear which value is meant.", IssueCode.DuplicateProperty);
                }

                SkipWhitespace();
                if (Peek() != ':') throw Fail("A colon was expected after the property name \"" + name + "\".");
                position++;

                value.Members.Add(new KeyValuePair<string, JsonValue>(name, ParseValue()));

                SkipWhitespace();
                char next = Peek();
                position++;
                if (next == '}') break;
                if (next != ',')
                {
                    position--;
                    throw Fail("A comma or a closing brace was expected here.");
                }
            }

            depth--;
            return value;
        }

        JsonValue ParseArray()
        {
            Enter();
            position++;
            var value = new JsonValue { Kind = JsonKind.Array, Items = new List<JsonValue>() };

            SkipWhitespace();
            if (Peek() == ']')
            {
                position++;
                depth--;
                return value;
            }

            while (true)
            {
                value.Items.Add(ParseValue());

                SkipWhitespace();
                char next = Peek();
                position++;
                if (next == ']') break;
                if (next != ',')
                {
                    position--;
                    throw Fail("A comma or a closing bracket was expected here.");
                }
            }

            depth--;
            return value;
        }

        string ParseString()
        {
            position++; // the opening quote
            var value = new StringBuilder();
            while (true)
            {
                if (position >= text.Length) throw Fail("The file ends inside a text value; a closing quote is missing.");

                char c = text[position++];
                if (c == '"') return value.ToString();
                if (c < ' ')
                {
                    position--;
                    throw Fail("A text value contains a raw control character, such as a line break. Write it as an escape.");
                }

                if (c != '\\')
                {
                    value.Append(c);
                    continue;
                }

                if (position >= text.Length) throw Fail("The file ends inside an escape sequence.");

                char escape = text[position++];
                switch (escape)
                {
                    case '"': value.Append('"'); break;
                    case '\\': value.Append('\\'); break;
                    case '/': value.Append('/'); break;
                    case 'b': value.Append('\b'); break;
                    case 'f': value.Append('\f'); break;
                    case 'n': value.Append('\n'); break;
                    case 'r': value.Append('\r'); break;
                    case 't': value.Append('\t'); break;
                    case 'u':
                        if (position + 4 > text.Length) throw Fail("The file ends inside a \\u escape.");
                        int code = 0;
                        for (int i = 0; i < 4; i++)
                        {
                            int digit = HexValue(text[position + i]);
                            if (digit < 0)
                            {
                                position += i;
                                throw Fail("A \\u escape needs four hexadecimal digits.");
                            }

                            code = code * 16 + digit;
                        }

                        position += 4;
                        value.Append((char)code);
                        break;
                    default:
                        position--;
                        throw Fail("\"\\" + escape + "\" is not a JSON escape sequence.");
                }
            }
        }

        JsonValue ParseNumber()
        {
            int start = position;
            if (Peek() == '-') position++;

            if (Peek() == '0')
            {
                position++;
                if (IsDigit(Peek()))
                {
                    position = start;
                    throw Fail("A number may not start with extra zeros.");
                }
            }
            else if (!SkipDigits())
            {
                throw Fail("A digit was expected in this number.");
            }

            if (Peek() == '.')
            {
                position++;
                if (!SkipDigits()) throw Fail("A digit was expected after the decimal point.");
            }

            if (Peek() == 'e' || Peek() == 'E')
            {
                position++;
                if (Peek() == '+' || Peek() == '-') position++;
                if (!SkipDigits()) throw Fail("A digit was expected in the exponent of this number.");
            }

            char after = Peek();
            if (after != '\0' && after != ',' && after != '}' && after != ']' && !IsWhitespace(after))
                throw Fail("Unexpected character '" + after + "' in a number. Numbers are written like 12, -0.5 or 1e3.");

            return new JsonValue { Kind = JsonKind.Number, Text = text.Substring(start, position - start) };
        }

        bool SkipDigits()
        {
            int start = position;
            while (IsDigit(Peek())) position++;
            return position > start;
        }

        bool TryWord(string word)
        {
            if (string.CompareOrdinal(text, position, word, 0, word.Length) != 0) return false;

            int end = position + word.Length;
            if (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_')) return false;

            position = end;
            return true;
        }

        void Enter()
        {
            if (++depth > MaxDepth) throw Fail("The file nests objects and lists far deeper than a mission needs.");
        }

        char Peek()
        {
            return position < text.Length ? text[position] : '\0';
        }

        void SkipWhitespace()
        {
            while (position < text.Length && IsWhitespace(text[position])) position++;
        }

        static bool IsWhitespace(char c)
        {
            return c == ' ' || c == '\t' || c == '\n' || c == '\r';
        }

        static bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }

        static int HexValue(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        JsonSyntaxException Fail(string message, IssueCode code = IssueCode.MalformedJson)
        {
            int line = 1;
            int column = 1;
            int end = Math.Min(position, text.Length);
            for (int i = 0; i < end; i++)
            {
                if (text[i] == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }
            }

            return new JsonSyntaxException(code, message, line, column);
        }
    }
}
