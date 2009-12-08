// Gardens Point Scanner Generator
// Copyright (c) K John Gough, QUT 2006-2008
// (see accompanying GPLEXcopyright.rtf.

//  Parse helper for bootstrap version of gplex
//  kjg 17 June 2006
//

using System;
using System.IO;
using System.Collections.Generic;
using QUT.Gplex.Lexer;
using QUT.GplexBuffers;

namespace QUT.Gplex.Parser
{
    internal delegate QUT.Gplex.Automaton.OptionState OptionParser2(string s);

    internal class LexSpan : QUT.Gppg.IMerge<LexSpan>
    {
        internal int startLine;       // start line of span
        internal int startColumn;     // start column of span
        internal int endLine;         // end line of span
        internal int endColumn;       // end column of span
        internal int startIndex;      // start position in the buffer
        internal int endIndex;        // end position in the buffer
        internal ScanBuff buffer;     // reference to the buffer

        public LexSpan() { }
        public LexSpan(int sl, int sc, int el, int ec, int sp, int ep, ScanBuff bf)
        { startLine = sl; startColumn = sc; endLine = el; endColumn = ec; startIndex = sp; endIndex = ep; buffer = bf; }

        /// <summary>
        /// This method implements the IMerge interface
        /// </summary>
        /// <param name="end">The last span to be merged</param>
        /// <returns>A span from the start of 'this' to the end of 'end'</returns>
        public LexSpan Merge(LexSpan end)
        {
            return new LexSpan(startLine, startColumn, end.endLine, end.endColumn, startIndex, end.endIndex, buffer); 
        }

        /// <summary>
        /// Get a short span from the first line of this span.
        /// </summary>
        /// <param name="idx">Starting index</param>
        /// <param name="len">Length of span</param>
        /// <returns></returns>
        internal LexSpan FirstLineSubSpan(int idx, int len)
        {
            //if (this.endLine != this.startLine) 
            //    throw new Exception("Cannot index into multiline span");

            return new LexSpan(
                this.startLine, this.startColumn + idx, this.startLine, this.startColumn + idx + len,
                this.startIndex, this.endIndex, this.buffer);
        }

        internal bool IsInitialized { get { return buffer != null; } }

        internal void StreamDump(TextWriter sWtr)
        {
            // int indent = sCol;
            int savePos = buffer.Pos;
            string str = buffer.GetString(startIndex, endIndex);
            //for (int i = 0; i < indent; i++)
            //    sWtr.Write(' ');
            sWtr.WriteLine(str);
            buffer.Pos = savePos;
            sWtr.Flush();
        }

        //internal void ConsoleDump()
        //{
        //    int savePos = buffer.Pos;
        //    string str = buffer.GetString(startIndex, endIndex);
        //    Console.WriteLine(str);
        //    buffer.Pos = savePos; 
        //}

        public override string ToString()
        {
            return buffer.GetString(startIndex, endIndex);
        }
    }

    internal partial class Parser
    {
        internal Parser(Scanner scanner) : base(scanner) { }

        static LexSpan blank = new LexSpan();  // marked by buff == null
        internal static LexSpan BlankSpan { get { return blank; } }
 
        ErrorHandler handler;
        StartStateScope scope = new StartStateScope();

        AAST aast;
        internal AAST Aast { get { return aast; } }

        OptionParser2 processOption2;

        RuleBuffer rb = new RuleBuffer();
        bool typedeclOK = true;
        bool isBar;

        /// <summary>
        /// The runtime parser support expects the scanner to be of 
        /// abstract AbstractScanner type. The abstract syntax tree object
        /// has a handle on the concrete objects so that semantic
        /// actions can get the extra functionality without a cast.
        /// </summary>
        /// <param name="scnr"></param>
        /// <param name="hdlr"></param>
        internal void Initialize(QUT.Gplex.Automaton.TaskState t, Scanner scnr, ErrorHandler hdlr, OptionParser2 dlgt)
        {
            this.handler = hdlr;
            this.aast = new AAST(t);
            this.aast.hdlr = hdlr;
            //this.aast.parser = this;
            this.aast.scanner = scnr;
            this.processOption2 = dlgt;
        }

        internal static AAST.Destination Dest
        {
            get { // only the first declaration can go in the usingDcl group
                return AAST.Destination.codeIncl;
            }
        }

        List<LexSpan> nameLocs = new List<LexSpan>();
        List<string> nameList = new List<string>();

        internal void AddName(LexSpan l)
        {
            nameLocs.Add(l);
            nameList.Add(aast.scanner.Buffer.GetString(l.startIndex, l.endIndex));
        }

        /// <summary>
        /// This method adds newly defined start condition names to the
        /// table of start conditions. 
        /// </summary>
        /// <param name="isExcl">True iff the start condition is exclusive</param>
        internal void AddNames(bool isExcl)
        {
            for (int i = 0; i < nameList.Count; i++)
            {
                string s = nameList[i];
                LexSpan l = nameLocs[i];
                if (Char.IsDigit(s[0])) handler.ListError(l, 72, s); 
                else if (!aast.AddStartState(isExcl, s)) handler.ListError(l, 50, s);
            }
            // And now clear the nameList
            nameList.Clear();
            nameLocs.Clear();
        }

        internal void AddCharSetPredicates()
        {
            for (int i = 0; i < nameList.Count; i++)
            {
                string s = nameList[i];
                LexSpan l = nameLocs[i];
                aast.AddLexCatPredicate(s, l);
            }
            // And now clear the nameList
            nameList.Clear();
            nameLocs.Clear();
        }

        /// <summary>
        /// Parse a line of option commands.  
        /// These may be either whitespace or comma separated
        /// </summary>
        /// <param name="l">The LexSpan of all the commands on this line</param>
        internal void ParseOption(LexSpan l)
        {
            char[] charSeparators = new char[] { ',', ' ', '\t' };
            string strn = aast.scanner.Buffer.GetString(l.startIndex, l.endIndex);
            string[] cmds = strn.Split(charSeparators, StringSplitOptions.RemoveEmptyEntries);
            foreach (string s in cmds)
            {
                Automaton.OptionState rslt = this.processOption2(s);
                switch (rslt)
                {
                    case Automaton.OptionState.clear:
                        break;
                    case Automaton.OptionState.errors:
                        handler.ListError(l, 74, s);
                        break;
                    case Automaton.OptionState.inconsistent:
                        handler.ListError(l, 84, s);
                        break;
                    case Automaton.OptionState.alphabetLocked:
                        handler.ListError(l, 83, s);
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// Used to validate used occurrences of start condition lists
        /// these may include the number '0' as a synonym for "INITIAL"
        /// </summary>
        /// <param name="lst">The list of supposed start state names</param>
        internal void AddNameListToStateList(List<StartState> lst)
        {
            for (int i = 0; i < nameList.Count; i++)
            {
                string s = nameList[i];
                LexSpan l = nameLocs[i];
                if (s.Equals("0")) s = "INITIAL";

                if (Char.IsDigit(s[0])) handler.ListError(l, 72, s); // Illegal name
                else
                {
                    StartState obj = aast.StartStateValue(s);
                    if (obj == null) handler.ListError(l, 51, s);
                    else lst.Add(obj);
                }
            }
            nameList.Clear();
            nameLocs.Clear();
        }

        internal void AddLexCategory(LexSpan nLoc, LexSpan vLoc)
        {
            // string name = aast.scanner.buffer.GetString(nVal.startIndex, nVal.endIndex + 1);
            string name = aast.scanner.Buffer.GetString(nLoc.startIndex, nLoc.endIndex);
            string verb = aast.scanner.Buffer.GetString(vLoc.startIndex, vLoc.endIndex);
            if (!aast.AddLexCategory(name, verb, vLoc))
                handler.ListError(nLoc, 52, name);
                // handler.AddError("Error: name " + name + " already defined", nLoc);
        }

    }

} // end of namespace
