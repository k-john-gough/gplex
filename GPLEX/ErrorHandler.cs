// Gardens Point Scanner Generator
// Copyright (c) K John Gough, QUT 2006-2010
// (see accompanying GPLEXcopyright.rtf)

using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using QUT.GplexBuffers;

namespace QUT.Gplex.Parser
{
    internal class Error : IComparable<Error>
    {
        internal const int minErr = 50;
        internal const int minWrn = 110;

        internal int code;
        internal bool isWarn;
        internal string message;
        internal LexSpan span;


        internal Error(int code, string msg, LexSpan spn, bool wrn)
        {
            this.code = code;
            isWarn = wrn;
            message = msg;
            span = spn;
        }

        public int CompareTo(Error r)
        {
            if (span.startLine < r.span.startLine) return -1;
            else if (span.startLine > r.span.startLine) return 1;
            else if (span.startColumn < r.span.startColumn) return -1;
            else if (span.startColumn > r.span.startColumn) return 1;
            else return 0;
        }
    }
    
    
    internal class ErrorHandler
    {
        const int maxErrors = 50; // Will this be enough for all users?

        List<Error> errors;
        int errNum;
        int wrnNum; 

        internal bool Errors { get { return errNum > 0; } }
        internal bool Warnings { get { return wrnNum > 0; } }


        internal int ErrNum { get { return errNum; } }
        internal int WrnNum { get { return wrnNum; } }

        internal ErrorHandler()
        {
            errors = new List<Error>(8);
        }
        // -----------------------------------------------------
        //   Public utility methods
        // -----------------------------------------------------

 
        internal List<Error> SortedErrorList()
        {
            if (errors.Count > 1) errors.Sort();
            return errors;
        }

        internal void AddError(string msg, LexSpan spn)
        {
            this.AddError(new Error(3, msg, spn, false)); errNum++;
        }

        private void AddError(Error e)
        {
            errors.Add(e);
            if (errors.Count > maxErrors)
            {
                errors.Add(new Error(2, "Too many errors, abandoning", e.span, false));
                throw new TooManyErrorsException("Too many errors");
            }
        }

        /// <summary>
        /// Add this error to the error buffer.
        /// </summary>
        /// <param name="spn">The span to which the error is attached</param>
        /// <param name="num">The error number</param>
        /// <param name="key">The featured string</param>
        internal void ListError(LexSpan spn, int num, string key, char quote)
        { ListError(spn, num, key, quote, quote); }
        internal void ListError(LexSpan spn, int num, string key)
        { ListError(spn, num, key, '<', '>'); }

        void ListError(LexSpan spn, int num, string key, char lh, char rh)
        {
            string prefix, suffix, message;
            switch (num)
            {
                case   1: prefix = "Parser error"; suffix = "";
                    break;
                case  50: prefix = "Start state"; suffix = "already defined";
                    break;
                case  51: prefix = "Start state"; suffix = "undefined";
                    break;
                case  52: prefix = "Lexical category"; suffix = "already defined";
                    break;
                case  53: prefix = "Expected character"; suffix = "";
                    break;
                case 55: prefix = "Unknown lexical category"; suffix = "";
                    break;
                case 61: prefix = "Missing matching construct"; suffix = "";
                    break;
                case 62: prefix = "Unexpected symbol, skipping to "; suffix = "";
                    break;
                case 70: prefix = "Illegal character escape "; suffix = "";
                    break;
                case 71: prefix = "Lexical category must be a character class "; suffix = "";
                    break;
                case 72: prefix = "Illegal name for start condition "; suffix = "";
                    break;
                case 74: prefix = "Unrecognized \"%option\" command "; suffix = "";
                    break;
                case 76: prefix = "Unknown character predicate"; suffix = "";
                    break;
                case 83: prefix = "Cannot set /unicode option inconsistently"; suffix = "";
                    break;
                case 84: prefix = "Inconsistent \"%option\" command "; suffix = "";
                    break;
                case 85: prefix = "Unicode literal too large:"; suffix = "use %option unicode";
                    break;
                case 86: prefix = "Illegal octal character escape "; suffix = "";
                    break;
                case 87: prefix = "Illegal hexadecimal character escape "; suffix = "";
                    break;
                case 88: prefix = "Illegal unicode character escape "; suffix = "";
                    break;
                case 96: prefix = "Class"; suffix = "not found in assembly"; 
                    break;
                case 97: prefix = "Method"; suffix = "not found in class";
                    break;
                case 99: prefix = "Illegal escape sequence"; suffix = "";
                    break;
                case 103: prefix = "Expected character with property"; suffix = "";
                    break;

                // Warnings ...

                case 111: prefix = "This char"; suffix = "does not need escape in character class";
                    break;
                case 113: prefix = "Special case:"; suffix = "included as set class member";
                    break;
                case 114: prefix = "No upper bound to range,"; suffix = "included as set class members";
                    break;
                case 116: prefix = "This pattern always overridden by"; suffix = ""; break;
                case 117: prefix = "This pattern always overrides"; suffix = ""; break;
                case 121: prefix = "char class"; suffix = ""; break;

                default: prefix = "Error " + Convert.ToString(num, CultureInfo.InvariantCulture); suffix = "";
                    break;
            }
            // message = prefix + " <" + key + "> " + suffix;
            message = String.Format(CultureInfo.InvariantCulture, "{0} {1}{2}{3} {4}", prefix, lh, key, rh, suffix);
            this.AddError(new Error(num, message, spn, num >= Error.minWrn)); 
            if (num < Error.minWrn) errNum++; else wrnNum++;
        }

        internal void ListError(LexSpan spn, int num)
        {
            string message;
            switch (num)
            {
                case 54: message = "Invalid character range: lower bound > upper bound"; break;
                case 56: message = "\"using\" is illegal, use \"%using\" instead"; break;
                case 57: message = "\"namespace\" is illegal, use \"%namespace\" instead"; break;
                case 58: message = "Type declarations impossible in this context"; break;
                case 59: message = "\"next\" action '|' cannot be used on last pattern"; break;
                case 60: message = "Unterminated block comment starts here"; break;
                case 63: message = "Invalid single-line action"; break;
                case 64: message = "This token unexpected"; break;
                case 65: message = "Invalid action"; break;
                case 66: message = "Missing comma in namelist"; break;
                case 67: message = "Invalid or empty namelist"; break;
                case 68: message = "Invalid production rule"; break;
                case 69: message = "Symbols '^' and '$' can only occur at the ends of patterns"; break;

                case 73: message = "No namespace has been defined"; break;
                case 75: message = "Context must have fixed right length or fixed left length"; break;
                case 77: message = "Unknown LEX tag name"; break;
                case 78: message = "Expected space here"; break;
                case 79: message = "Illegal character in this context"; break;

                case 80: message = "Expected end-of-line here"; break;
                case 81: message = "Invalid character range, no upper bound character"; break;
                case 82: message = "Invalid class character: '-' must be \\escaped"; break;
                case 89: message = "Empty semantic action, must be at least a comment"; break;

                case 90: message = "\"%%\" marker must start at beginning of line"; break;
                case 91: message = "Version of gplexx.frame is not recent enough"; break;
                case 92: message = "Non unicode scanners only allow single-byte codepages"; break;
                case 93: message = "Non unicode scanners cannot use /codepage:guess"; break;
                case 94: message = "This assembly could not be found"; break;
                case 95: message = "This assembly could not be loaded"; break;
                case 98: message = "Only \"public\" or \"internal\" allowed here"; break;

                case 100: message = "Context operator cannot be used with right anchor '$'"; break;
                case 101: message = "Extra characters at end of regular expression"; break;
                case 102: message = "Literal string terminated by end of line"; break;

                // Warnings ...

                case 110: message = "Code between rules, ignored"; break;
                case 112: message = "/babel option is unsafe without /unicode option"; break;
                case 115: message = "This pattern matches \"\", and might loop"; break;
                case 116: message = "This pattern is never matched"; break;
                case 117: message = "This constructed set is empty"; break;

                default:  message = "Error " + Convert.ToString(num, CultureInfo.InvariantCulture); break;
            }
            this.AddError(new Error(num, message, spn, num >= Error.minWrn));
            if (num < Error.minWrn) errNum++; else wrnNum++;
        }
 
        
        // -----------------------------------------------------
        //   Error Listfile Reporting Method
        // -----------------------------------------------------

        internal void MakeListing(ScanBuff buff, StreamWriter sWrtr, string name, string version)
        {
            int line = 1;
            int eNum = 0;
            int eLin = 0;

            int nxtC = (int)'\n';
            int groupFirst;
            int currentCol;
            int currentLine;

            //
            //  Errors are sorted by line number
            //
            errors = SortedErrorList();
            //
            //  Reset the source file buffer to the start
            //
            buff.Pos = 0;
            sWrtr.WriteLine(); 
            ListDivider(sWrtr);
            sWrtr.WriteLine("//  GPLEX error listing for lex source file <"
                                                           + name + ">");
            ListDivider(sWrtr);
            sWrtr.WriteLine("//  Version:  " + version);
            sWrtr.WriteLine("//  Machine:  " + Environment.MachineName);
            sWrtr.WriteLine("//  DateTime: " + DateTime.Now.ToString());
            sWrtr.WriteLine("//  UserName: " + Environment.UserName);
            ListDivider(sWrtr); sWrtr.WriteLine(); sWrtr.WriteLine();
            //
            //  Initialize the error group
            //
            groupFirst = 0;
            currentCol = 0;
            currentLine = 0;
            //
            //  Now, for each error do
            //
            for (eNum = 0; eNum < errors.Count; eNum++)
            {
                Error errN = errors[eNum];
                eLin = errN.span.startLine;
                if (eLin > currentLine)
                {
                    //
                    // Spill all the waiting messages
                    //
                    int maxGroupWidth = 0;
                    if (currentCol > 0)
                    {
                        sWrtr.WriteLine();
                        currentCol = 0;
                    }
                    for (int i = groupFirst; i < eNum; i++)
                    {
                        Error err = errors[i];
                        string prefix = (err.isWarn ? "// Warning: " : "// Error: ");
                        string msg = StringUtilities.MakeComment(3, prefix + err.message);
                        if (StringUtilities.MaxWidth(msg) > maxGroupWidth)
                            maxGroupWidth = StringUtilities.MaxWidth(msg);
                        sWrtr.Write(msg);
                        sWrtr.WriteLine();
                    }
                    if (groupFirst < eNum)
                    {
                        sWrtr.Write("// ");
                        Spaces(sWrtr, maxGroupWidth - 3);
                        sWrtr.WriteLine();
                    }
                    currentLine = eLin;
                    groupFirst = eNum;
                }
                //
                //  Emit lines up to *and including* the error line
                //
                while (line <= eLin)
                {
                    nxtC = buff.Read();
                    if (nxtC == (int)'\n')
                        line++;
                    else if (nxtC == ScanBuff.EndOfFile)
                        break;
                    sWrtr.Write((char)nxtC);
                }
                //
                //  Now emit the error message(s)
                //
                if (errN.span.endColumn > 3 && errN.span.startColumn < 80)
                {
                    if (currentCol == 0)
                    {
                        sWrtr.Write("//");
                        currentCol = 2;
                    }
                    if (errN.span.startColumn > currentCol)
                    {
                        Spaces(sWrtr, errN.span.startColumn - currentCol);
                        currentCol = errN.span.startColumn;
                    }
                    for (; currentCol < errN.span.endColumn && currentCol < 80; currentCol++)
                        sWrtr.Write('^');
                }
            }
            //
            //  Clean up after last message listing
            //  Spill all the waiting messages
            //
            int maxEpilogWidth = 0;
            if (currentCol > 0)
            {
                sWrtr.WriteLine();
            }
            for (int i = groupFirst; i < errors.Count; i++)
            {
                Error err = errors[i];
                string prefix = (err.isWarn ? "// Warning: " : "// Error: ");
                string msg = StringUtilities.MakeComment(3, prefix + err.message);
                if (StringUtilities.MaxWidth(msg) > maxEpilogWidth)
                    maxEpilogWidth = StringUtilities.MaxWidth(msg);
                sWrtr.Write(msg);
                sWrtr.WriteLine();
            }
            if (groupFirst < errors.Count)
            {
                sWrtr.Write("// ");
                Spaces(sWrtr, maxEpilogWidth - 3);
                sWrtr.WriteLine();
            }
            //
            //  And dump the tail of the file
            //
            nxtC = buff.Read();
            while (nxtC != ScanBuff.EndOfFile)
            {
                sWrtr.Write((char)nxtC);
                nxtC = buff.Read();
            }
            ListDivider(sWrtr); sWrtr.WriteLine();
            sWrtr.Flush();
            // sWrtr.Close();
        }

        private static void ListDivider(StreamWriter wtr)
        {
            wtr.WriteLine(
            "// =========================================================================="
            );
        }

        private static void Spaces(StreamWriter wtr, int len)
        {
            for (int i = 0; i < len; i++) wtr.Write('-');
        }


        // -----------------------------------------------------
        //   Console Error Reporting Method
        // -----------------------------------------------------

        internal void DumpErrorsInMsbuildFormat( ScanBuff buff, TextWriter wrtr ) {
            StringBuilder builder = new StringBuilder();
            //
            // Message prefix
            //
            string location = (buff != null ? buff.FileName : "GPLEX");
            foreach (Error err in errors) {
                builder.Length = 0; // Works for V2.0 even.
                //
                // Origin
                //
                builder.Append( location );
                if (buff != null) {
                    builder.Append( '(' );
                    builder.Append( err.span.startLine );
                    builder.Append( ',' );
                    builder.Append( err.span.startColumn );
                    builder.Append( ')' );
                }
                builder.Append( ':' );
                //
                // Category                builder.Append( ':' );
                //
                builder.Append( err.isWarn ? "warning " : "error " );
                builder.Append( err.code );
                builder.Append( ':' );
                //
                // Message
                //
                builder.Append( err.message );
                Console.Error.WriteLine( builder.ToString() );
            }
        }



        internal void DumpAll(ScanBuff buff, TextWriter wrtr) {
            int  line = 1;
            int  eNum = 0;
            int  eLin = 0;
            int nxtC = (int)'\n'; 
            //
            //  Initialize the error group
            //
            int groupFirst = 0;
            int currentCol = 0;
            int currentLine = 0;
            //
            //  Reset the source file buffer to the start
            //
            buff.Pos = 0;
            wrtr.WriteLine("Error Summary --- ");
            //
            //  Initialize the error group
            //
            groupFirst = 0;
            currentCol = 0;
            currentLine = 0;
            //
            //  Now, for each error do
            //
            for (eNum = 0; eNum < errors.Count; eNum++) {
                eLin = errors[eNum].span.startLine;
                if (eLin > currentLine) {
                    //
                    // Spill all the waiting messages
                    //
                    if (currentCol > 0) {
                        wrtr.WriteLine();
                        currentCol = 0;
                    }
                    for (int i = groupFirst; i < eNum; i++) {
                        Error err = errors[i];
                        wrtr.Write((err.isWarn ? "Warning: " : "Error: "));
                        wrtr.Write(err.message);    
                        wrtr.WriteLine();    
                    }
                    currentLine = eLin;
                    groupFirst  = eNum;
                } 
                //
                //  Skip lines up to *but not including* the error line
                //
                while (line < eLin) {
                    nxtC = buff.Read();
                    if (nxtC == (int)'\n') line++;
                    else if (nxtC == ScanBuff.EndOfFile) break;
                } 
                //
                //  Emit the error line
                //
                if (line <= eLin) {
                    wrtr.Write((char)((eLin/1000)%10+(int)'0'));
                    wrtr.Write((char)((eLin/100)%10+(int)'0'));
                    wrtr.Write((char)((eLin/10)%10+(int)'0'));
                    wrtr.Write((char)((eLin)%10+(int)'0'));
                    wrtr.Write(' ');
                    while (line <= eLin) {
                        nxtC = buff.Read();
                        if (nxtC == (int)'\n') line++;
                        else if (nxtC == ScanBuff.EndOfFile) break;
                        wrtr.Write((char)nxtC);
                    } 
                } 
                //
                //  Now emit the error message(s)
                //
                if (errors[eNum].span.startColumn >= 0 && errors[eNum].span.startColumn < 75) {
                    if (currentCol == 0) {
                        wrtr.Write("-----");
                    }
                    for (int i = currentCol; i < errors[eNum].span.startColumn; i++, currentCol++) {
                        wrtr.Write('-');
                    }
                    for (; currentCol < errors[eNum].span.endColumn && currentCol < 75; currentCol++)
                        wrtr.Write('^');
                }
            }
            //
            //  Clean up after last message listing
            //  Spill all the waiting messages
            //
            if (currentCol > 0) {
                wrtr.WriteLine();
            }
            for (int i = groupFirst; i < errors.Count; i++) {
                Error err = errors[i];
                wrtr.Write((err.isWarn ? "Warning: " : "Error: "));
                wrtr.Write(errors[i].message);    
                wrtr.WriteLine();    
            }
        } 
    }
}