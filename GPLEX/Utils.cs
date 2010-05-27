//
// Gardens Point Scanner Generator
// Copyright (c) K John Gough, QUT 2006-2010
// (see accompanying GPLEXcopyright.rtf)
// 
// These utilities are used in GPLEX and GPPG
//

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using System.Globalization;

namespace QUT.Gplex.Parser
{

    /// <summary>
    /// This class supplies character escape 
    /// utilities for project.
    /// </summary>
    public static class CharacterUtilities
    {
        #region Character escape utilities
        static int symLimit = 255; // default is 8-bit
        public static void SetUnicode() { symLimit = 0x10ffff; }
        public static int SymLimit { get { return symLimit; } }
        //
        // Utility procedures for handling numeric character escapes
        // and mapping the usual ANSI C escapes.
        //
        private static bool IsOctDigit(char ch) { return ch >= '0' && ch <= '7'; }

        private static char OctChar(char a, char b, char c)
        {
            int x = (int)a - (int)'0';
            int y = (int)b - (int)'0';
            int z = (int)c - (int)'0';
            return (char)(((x * 8) + y) * 8 + z);
        }

        /// <summary>
        /// This assumes that a,b are HexDigit characters
        /// </summary>
        /// <param name="a">most significant digit</param>
        /// <param name="b">less significant digit</param>
        /// <returns></returns>
        private static char HexChar(char a, char b)
        {
            int x = (char.IsDigit(a) ?
                (int)a - (int)'0' :
                (int)char.ToLower(a, CultureInfo.InvariantCulture) + (10 - (int)'a'));
            int y = (char.IsDigit(b) ?
                (int)b - (int)'0' :
                (int)char.ToLower(b, CultureInfo.InvariantCulture) + (10 - (int)'a'));
            return (char)(x * 16 + y);
        }

        private static int ValOfHex(char x)
        {
            return ((x >= '0' && x <= '9') ?
                (int)x - (int)'0' :
                (int)char.ToLower(x, CultureInfo.InvariantCulture) + (10 - (int)'a'));
        }

        private static bool IsHexDigit(char ch)
        {
            return ch >= '0' && ch <= '9' || ch >= 'a' && ch <= 'f' || ch >= 'A' && ch <= 'F';
        }

        /// <summary>
        /// Get a substring of length len from the input string 
        /// input, starting from the index ix. If there are not 
        /// len characters left return rest of string.
        /// </summary>
        /// <param name="input">the input string</param>
        /// <param name="ix">the starting index</param>
        /// <param name="len">the requested length</param>
        /// <returns></returns>
        private static string GetSubstring(string input, int ix, int len)
        {
            if (input.Length - ix >= len)
                return input.Substring(ix, len);
            else
                return input.Substring(ix);
        }

        /// <summary>
        /// Assert: special case of '\0' is already filtered out.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private static int GetOctalChar(string str)
        {
            if (str.Length != 3)
                return -1;
            if (IsOctDigit(str[0]) && IsOctDigit(str[1]) && IsOctDigit(str[2]))
                return OctChar(str[0], str[1], str[2]);
            else
                return -1;
        }

        private static int GetHexadecimalChar(string str)
        {
            if (str.Length != 2)
                return -1;
            if (IsHexDigit(str[0]) && IsHexDigit(str[1]))
                return HexChar(str[0], str[1]);
            else
                return -1;
        }

        private static int GetUnicodeChar(string str)
        {
            uint rslt = 0;
            int length = str.Length;
            if (length != 4 && length != 8)
                return -1;
            for (int i = 0; i < str.Length; i++)
            {
                if (!IsHexDigit(str[i]))
                    return -1;
                rslt = rslt * 16 + (uint)ValOfHex(str[i]);
            }
            return (int)rslt;
        }

        /// <summary>
        /// This static method expands characters in a literal 
        /// string, returning a modified string with character
        /// escapes replaced by the character which they denote.
        /// This includes characters outside the BMP which 
        /// return a pair of surrogate characters.
        /// </summary>
        /// <param name="str">the input string</param>
        /// <returns>interpreted version of the string</returns>
        public static string InterpretCharacterEscapes(string source)
        {
            int sLen = source.Length;
            if (sLen == 0)
                return source;
            char[] arr = new char[sLen];
            int sNxt = 0;
            int aNxt = 0;
            char chr = source[sNxt++];
            for (; ; chr = source[sNxt++])
            {
                if (chr != '\\')
                    arr[aNxt++] = chr;
                else
                {
                    int codePoint = EscapedChar(source, ref sNxt);
                    if (codePoint > 0xFFFF)
                    {
                        arr[aNxt++] = CharacterUtilities.HiSurrogate(codePoint);
                        arr[aNxt++] = CharacterUtilities.LoSurrogate(codePoint);
                    }
                    else
                        arr[aNxt++] = (char)codePoint;
                }
                if (sNxt == sLen)
                    return new String(arr, 0, aNxt);
            }
        }

        /// <summary>
        /// Take a possibly escaped character literal and
        /// convert it to a canonical form.  Thus @"'\377'",
        /// @"'\xff'" and @"'\u00FF'" all return @"'\xFF'".
        /// Throws a StringInterpretException if character
        /// ordinal exceeds "symLimit".
        /// </summary>
        /// <param name="str">The string representation</param>
        /// <param name="startIx">start index of character</param>
        /// <returns>canonical representation of character</returns>
        public static string Canonicalize(string source, int startIndex)
        {
            char chr0 = source[startIndex++];
            if (chr0 != '\\')
                return String.Format(CultureInfo.InvariantCulture, "'{0}'", chr0);
            else
            {
                int codePoint = EscapedChar(source, ref startIndex);
                return QuoteMap(codePoint);
            }
        }

        /// <summary>
        /// Take the string representation of a possibly backslash-
        /// escaped character literal and return the character ordinal.
        /// Throws a StringInterpretException if character
        /// ordinal exceeds "symLimit".
        /// </summary>
        /// <param name="str">the string representation</param>
        /// <param name="offset">start index of character</param>
        /// <returns>code point of character</returns>
        public static int OrdinalOfCharLiteral(string source, int offset)
        {
            char chr0 = source[offset++];
            if (chr0 != '\\')
                return (int)chr0;
            else
                return EscapedChar(source, ref offset);
        }

        /// <summary>
        /// This static method expands characters in a verbatim
        /// string, returning a modified string with character
        /// escapes replaced by the character which they denote.
        /// </summary>
        /// <param name="str">the input string</param>
        /// <returns>interpreted version of the string</returns>
        public static string InterpretEscapesInVerbatimString(string source)
        {
            int sLen = source.Length;
            if (sLen == 0)
                return source;
            char[] arr = new char[sLen];
            int sNxt = 0;
            int aNxt = 0;
            char chr = source[sNxt++];
            for (; ; chr = source[sNxt++])
            {
                if (chr != '\\')
                    arr[aNxt++] = chr;
                if (sNxt == sLen)
                    return new String(arr, 0, aNxt);
            }
        }

        /// <summary>
        /// Find the character denoted by the character escape
        /// starting with the backslash at position (index - 1).
        /// Postcondition: str[index] is the first character
        /// beyond the character escape denotation.
        /// </summary>
        /// <param name="str">the string to parse</param>
        /// <param name="index">the in-out index</param>
        /// <returns>the character denoted by the escape</returns>
        [SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "1#")]
        // Reason for message suppression: this is the cleanest way 
        // of moving the read index a variable number of positions
        public static int EscapedChar(string source, ref int index)
        {
            char chr = source[index++];
            int valu;
            switch (chr)
            {
                case '\\': return (int)'\\';
                case 'a': return (int)'\a';
                case 'b': return (int)'\b';
                case 'f': return (int)'\f';
                case 'n': return (int)'\n';
                case 'r': return (int)'\r';
                case 't': return (int)'\t';
                case 'v': return (int)'\v';

                case '0':
                case '1':
                case '2':
                case '3':
                    if (chr == '0' && !IsOctDigit(source[index]))
                        return (int)'\0';
                    valu = GetOctalChar(GetSubstring(source, index - 1, 3)); index += 2;
                    if (valu >= 0)
                        return valu;
                    else
                        throw new StringInterpretException("Invalid escape", GetSubstring(source, index - 4, 4));
                case 'x':
                case 'X':   // Just being nice here.
                    valu = GetHexadecimalChar(GetSubstring(source, index, 2)); index += 2;
                    if (valu >= 0)
                        return valu;
                    else
                        throw new StringInterpretException("Invalid escape", GetSubstring(source, index - 4, 4));
                case 'u':
                    valu = GetUnicodeChar(GetSubstring(source, index, 4)); index += 4;
                    if (valu < 0)
                        throw new StringInterpretException("Invalid escape", GetSubstring(source, index - 6, 6));
                    else
                        return valu;
                case 'U':
                    valu = GetUnicodeChar(GetSubstring(source, index, 8)); index += 8;
                    if (valu < 0)
                        throw new StringInterpretException("Invalid escape", GetSubstring(source, index - 10, 10));
                    else
                        return valu;
                default:
                    return (int)chr;
            }
        }

        /// <summary>
        /// Map literal characters into the display form 
        /// </summary>
        /// <param name="chr">the character to encode</param>
        /// <returns>the string denoting chr</returns>
        public static string Map(int code)
        {
            if (code > (int)' ' && code < 127 && code != '\\') return new String((char)code, 1);
            switch (code)
            {
                case (int)'\0': return "\\0";
                case (int)'\n': return "\\n";
                case (int)'\t': return "\\t";
                case (int)'\r': return "\\r";
                case (int)'\\': return "\\\\";
                default:
                    if (code < 256)
                        return String.Format(CultureInfo.InvariantCulture, "\\x{0:X2}", code);
                    else if (code <= UInt16.MaxValue) // use unicode literal
                        return String.Format(CultureInfo.InvariantCulture, "\\u{0:X4}", (ushort)code);
                    else
                        return String.Format(CultureInfo.InvariantCulture, "\\U{0:X8}", code);
            }
        }

        /// <summary>
        /// Same as Map(code) except '-' must be escaped.
        /// </summary>
        /// <param name="code">Unicode codepoint</param>
        /// <returns></returns>
        public static string MapForCharSet(int code)
        {
            return (code == (int)'-' ? "\\-" : Map(code));
        }

        /// <summary>
        /// Map string characters into the display form 
        /// </summary>
        /// <param name="chr">the character to encode</param>
        /// <returns>the string denoting chr</returns>
        private static string StrMap(int chr)
        {
            if (chr == '"') return "\\\"";
            else return Map(chr);
        }

        public static string QuoteMap(int code)
        {
            return String.Format(CultureInfo.InvariantCulture, "'{0}'", Map(code));
        }

        public static string QuoteMap(string source)
        {
            return String.Format(CultureInfo.InvariantCulture, "\"{0}\"", Map(source));
        }

        public static string Map(string source)
        {
            string rslt = "";
            if (source != null)
            {
                int index = 0;
                for (int point; (point = CodePoint(source, ref index)) != -1; )
                    rslt += StrMap(point);
            }
            return rslt;
        }

        /// <summary>
        /// Find the code point of the character starting at
        /// offset index in the string.  The code point may be
        /// beyond the BMP, and take up two chars in the string.
        /// </summary>
        /// <param name="pattern">The input string</param>
        /// <param name="index">The starting offset</param>
        /// <returns>The code point, as an int32</returns>
        [SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "1#")]
        // Reason for message suppression: this is the cleanest way 
        // of moving the read index a variable number of positions
        public static int CodePoint(string pattern, ref int index)
        {
            if (index >= pattern.Length)
                return -1;

            int hiUtf16 = (int)pattern[index++];
            if (hiUtf16 < 0xD800 || hiUtf16 > 0xDBFF)
                return hiUtf16;
            else
            {
                int loUtf16 = (int)pattern[index++];
                if (loUtf16 < 0xDC00 || loUtf16 > 0xDFFF)
                    throw new ArgumentException("Invalid surrogate pair");
                else
                    return (0x10000 + ((hiUtf16 & 0x3FF) << 10) + (loUtf16 & 0x3FF));
            }
        }

        private static char HiSurrogate(int codePoint)
        {
            return (char)(0xD800 + (codePoint - 0x10000) / 1024);
        }

        private static char LoSurrogate(int codePoint)
        {
            return (char)(0xDC00 + (codePoint - 0x10000) % 1024);
        }

        #endregion Character escape utilities
    }

    public static class StringUtilities
    {
        /// <summary>
        /// Modifies a string so that if it contains multiple lines
        /// every line after the first is prepended by "//" followed
        /// by "indent" spaces.
        /// </summary>
        /// <param name="indent">The line indent</param>
        /// <param name="text">The input string</param>
        /// <returns>The modified string</returns>
        public static string MakeComment(int indent, string text)
        {
            string EOLmark = System.Environment.NewLine + new String(' ', indent) + "// ";
            return text.Replace(System.Environment.NewLine, EOLmark);
        }

        /// <summary>
        /// Returns the character width of a (possibly multiline) string.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static int MaxWidth(string source)
        {
            int rslt = 0;
            string[] lines = source.Split(new Char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
                if (line.Length > rslt)
                    rslt = line.Length;
            return rslt;
        }
    }

    [Serializable]
    public class StringInterpretException : Exception
    {
        [NonSerialized]
        readonly string key;
        public string Key { get { return key; } }

        public StringInterpretException() { }
        public StringInterpretException(string text) : base(text) { }
        public StringInterpretException(string text, string key) : base(text) { this.key = key; }
        public StringInterpretException(string message, Exception inner) : base(message, inner) { }
        protected StringInterpretException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }

    }

    [Serializable]
    public class GplexInternalException : Exception
    {
        public GplexInternalException() { }
        public GplexInternalException(string message) : base(message) { }
        public GplexInternalException(string message, Exception inner) : base(message, inner) { }
        protected GplexInternalException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }

    [Serializable]
    public class TooManyErrorsException : Exception
    {
        public TooManyErrorsException() { }
        public TooManyErrorsException(string message) : base(message) { }
        public TooManyErrorsException(string message, Exception inner) : base(message, inner) { }
        protected TooManyErrorsException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}
