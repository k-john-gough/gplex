// Gardens Point Scanner Generator
// Copyright (c) K John Gough, QUT 2006-2010
// (see accompanying GPLEXcopyright.rtf)


using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using QUT.Gplex;

namespace QUT.Gplex.Parser
{
    /// <summary>
    /// This class represents the Attributed Abstract Syntax Tree
    /// corresponding to an input LEX file.
    /// </summary>
	internal sealed class AAST
	{
        internal QUT.Gplex.Lexer.Scanner scanner;
        internal ErrorHandler hdlr;

        private List<LexSpan> prolog   = new List<LexSpan>();   // Verbatim declarations for scanning routine
        private List<LexSpan> epilog   = new List<LexSpan>();   // Epilog code for the scanning routine
        private List<LexSpan> codeIncl = new List<LexSpan>();   // Text to copy verbatim into output file

        internal List<LexSpan> usingStrs = new List<LexSpan>();  // "using" dotted names
        internal LexSpan nameString;                             // Namespace dotted name
        private LexSpan userCode;                                // Text from the user code section

        internal string visibility = "public";                   // Visibility name "public" or "internal"
        internal string scanBaseName = "ScanBase";               // Name of scan base class.
        internal string tokenTypeName = "Tokens";                // Name of the token enumeration type.
        internal string scannerTypeName = "Scanner";             // Name of the generated scanner class.

        internal List<RuleDesc> ruleList = new List<RuleDesc>();
        internal List<LexCategory> lexCatsWithPredicates = new List<LexCategory>();
        Dictionary<string, LexCategory> lexCategories = new Dictionary<string, LexCategory>();
        internal Dictionary<string, PredicateLeaf> cats; // Allocated on demand
        internal Dictionary<string, StartState> startStates = new Dictionary<string, StartState>();
        private List<StartState> inclStates = new List<StartState>();

        Automaton.TaskState task;

        internal Automaton.TaskState Task { get { return task; } }
        internal int CodePage { get { return task.CodePage; } }
        internal bool IsVerbose { get { return task.Verbose; } }
        internal bool HasPredicates { get { return lexCatsWithPredicates.Count > 0; } }

        internal enum Destination {scanProlog, scanEpilog, codeIncl}

		internal AAST(Automaton.TaskState t) {
            task = t;
            startStates.Add(StartState.initState.Name, StartState.initState);
            startStates.Add(StartState.allState.Name, StartState.allState);
		}

        internal LexSpan UserCode
        {
            get { return userCode; }
            set { userCode = value; }
        }

        internal List<LexSpan> CodeIncl { get { return codeIncl; } }
        internal List<LexSpan> Prolog  { get { return prolog; } }
        internal List<LexSpan> Epilog { get { return epilog; } }

        internal void AddCodeSpan(Destination dest, LexSpan span)
        {
            if (!span.IsInitialized) return;
            switch (dest)
            {
                case Destination.codeIncl: CodeIncl.Add(span); break;
                case Destination.scanProlog: Prolog.Add(span); break;
                case Destination.scanEpilog: Epilog.Add(span); break;
            }
        }

        internal void AddVisibility(LexSpan span)
        {
            string result = span.ToString();
            if (result.Equals("internal") || result.Equals("public"))
                visibility = result;
            else
                hdlr.ListError(span, 98); 
        }

        internal void SetScanBaseName(string name)
        {
            scanBaseName = name;
        }

        internal void SetTokenTypeName(string name)
        {
            tokenTypeName = name;
        }

        internal void SetScannerTypeName(string name)
        {
            scannerTypeName = name;
        }
       

        internal bool AddLexCategory(string name, string verb, LexSpan spn)
        {
            if (lexCategories.ContainsKey(name))
                return false;
            else
            {
                LexCategory cls = new LexCategory(name, verb, spn);
                lexCategories.Add(name, cls);
                cls.ParseRE(this);
                return true;
            }
        }

        internal void AddLexCatPredicate(string name, LexSpan span)
        {
            LexCategory cat;
            if (!lexCategories.TryGetValue(name, out cat))
                hdlr.ListError(span, 55, name);
            else if (cat.regX.op != RegOp.charClass)
                hdlr.ListError(span, 71, name);
            else if (!cat.HasPredicate)
            {
                cat.HasPredicate = true;
                lexCatsWithPredicates.Add(cat);
                // Add a dummy exclusive start state for the predicate
                AddDummyStartState(cat.PredDummyName);
            }
        }

        //internal bool LookupLexCategory(string name)
        //{ return lexCategories.ContainsKey(name); }

        internal bool AddStartState(bool isX, string name)
        {
            return AddStartState(isX, false, name);
        }

        internal void AddDummyStartState(string name)
        {
            AddStartState(true, true, name);
        }

        bool AddStartState(bool isX, bool isDummy, string name)
        {
            if (name != null)
                if (startStates.ContainsKey(name))
                    return false;
                else
                {
                    StartState state = new StartState(isDummy, name);
                    startStates.Add(name, state);
                    if (!isX)
                        inclStates.Add(state);
                }
            return true;
        }

        internal StartState StartStateValue(string name)
        {
            StartState state;
            return (startStates.TryGetValue(name, out state) ? state : null);
        }

        internal int StartStateCount { get { return startStates.Count; } }

        internal void AddToAllStates(RuleDesc rule)
        {
            foreach (KeyValuePair<string, StartState> p in startStates)
            {
                StartState s = p.Value;
                if (!s.IsAll && !s.IsDummy) 
                    s.AddRule(rule);
            }
        }

        internal void FixupBarActions()
        {
            foreach (LexCategory cat in this.lexCatsWithPredicates)
                ruleList.Add(RuleDesc.MkDummyRuleDesc(cat, this));

            LexSpan lastSpan = Parser.BlankSpan;
            for (int i = ruleList.Count-1; i >= 0; i--)
            {
                RuleDesc rule = ruleList[i];
                if (!rule.isBarAction) lastSpan = rule.aSpan;
                else if (!lastSpan.IsInitialized)
                    hdlr.ListError(rule.pSpan, 59);
                else rule.aSpan = lastSpan;
                AddRuleToList(rule);
                // Now give the optional warning for
                // patterns that consume no input text.
                if (/* task.Verbose && */ rule.IsLoopRisk)
                        hdlr.ListError(rule.pSpan, 115);
            }
        }

        /// <summary>
        /// This method lazily constructs the dictionary for the
        /// character predicates.  Beware however, that this just
        /// maps the first "crd" characters of the unicode value set.
        /// </summary>
        private void InitCharCats()
        {
            cats = new Dictionary<string, PredicateLeaf>();
            cats.Add("IsControl", new PredicateLeaf(PredicateLeaf.MkCharTest(Char.IsControl, Char.IsControl)));
            cats.Add("IsDigit", new PredicateLeaf(PredicateLeaf.MkCharTest(Char.IsDigit, Char.IsDigit)));
            cats.Add("IsLetter", new PredicateLeaf(PredicateLeaf.MkCharTest(Char.IsLetter, Char.IsLetter)));
            cats.Add("IsLetterOrDigit", new PredicateLeaf(PredicateLeaf.MkCharTest(Char.IsLetterOrDigit, Char.IsLetterOrDigit)));
            cats.Add("IsLower", new PredicateLeaf(PredicateLeaf.MkCharTest(Char.IsLower, Char.IsLower)));
            cats.Add("IsNumber", new PredicateLeaf(PredicateLeaf.MkCharTest(Char.IsNumber, Char.IsNumber)));
            cats.Add("IsPunctuation", new PredicateLeaf(PredicateLeaf.MkCharTest(Char.IsPunctuation, Char.IsPunctuation)));
            cats.Add("IsSeparator", new PredicateLeaf(PredicateLeaf.MkCharTest(Char.IsSeparator, Char.IsSeparator)));
            cats.Add("IsSymbol", new PredicateLeaf(PredicateLeaf.MkCharTest(Char.IsSymbol, Char.IsSymbol)));
            cats.Add("IsUpper", new PredicateLeaf(PredicateLeaf.MkCharTest(Char.IsUpper, Char.IsUpper)));
            cats.Add("IsWhiteSpace", new PredicateLeaf(PredicateLeaf.MkCharTest(Char.IsWhiteSpace, Char.IsWhiteSpace)));
            cats.Add("IsFormatCharacter", new PredicateLeaf(PredicateLeaf.MkCharTest(CharCategory.IsFormat, CharCategory.IsFormat)));
            cats.Add("IdentifierStartCharacter", new PredicateLeaf(PredicateLeaf.MkCharTest(CharCategory.IsIdStart, CharCategory.IsIdStart)));
            cats.Add("IdentifierPartCharacter", new PredicateLeaf(PredicateLeaf.MkCharTest(CharCategory.IsIdPart, CharCategory.IsIdPart)));
            // IdentifierPartCharacters actually include the Format category
            // as well, but are kept separate here so we may attach a different
            // semantic action to identifiers that require canonicalization by
            // the elision of format characters, or the expansion of escapes.
        }


        private void AddUserPredicate(string name, CharTest test)
        {   
            if (this.cats == null) 
                InitCharCats();
            cats.Add(name, new PredicateLeaf(test));
        }

        /// <summary>
        /// Add a user-specified character predicate to the 
        /// dictionary. The predicate is in some known assembly
        /// accessible to gplex.
        /// </summary>
        /// <param name="name">the gplex name of the predicate</param>
        /// <param name="aSpan">the simple filename of the assembly</param>
        /// <param name="mSpan">the qualified name of the method</param>
        internal void AddUserPredicate(
            string name, 
            LexSpan aSpan, 
            LexSpan mSpan)
        {
            // maybe we need (1) type dotted name, (2) method name only?
            string mthName = mSpan.ToString();
            string asmName = aSpan.ToString();
            int offset = mthName.LastIndexOf('.');
            string clsName = mthName.Substring(0, offset);
            string mthIdnt = mthName.Substring(offset + 1);

            try
            {
                System.Reflection.Assembly asm = System.Reflection.Assembly.LoadFrom(asmName);
                System.Type[] types = asm.GetExportedTypes();
                foreach (Type type in types)
                {
                    if (type.FullName.Equals(clsName, StringComparison.OrdinalIgnoreCase) ||
                        type.Name.Equals(clsName, StringComparison.OrdinalIgnoreCase))
                    {
                        QUT.Gplex.ICharTestFactory factory =
                            (ICharTestFactory)System.Activator.CreateInstance(type);
          
                        if (factory != null)
                        {
                            CharTest test = factory.GetDelegate(mthIdnt);
                            if (test == null)
                                hdlr.ListError(mSpan, 97, mthIdnt);
                            else
                                AddUserPredicate(name, test);
                            return;
                        }
                    }
                    hdlr.ListError(mSpan, 96, clsName); return;
                }
            }
            catch (FileNotFoundException) { hdlr.ListError(aSpan, 94); }
            catch (FileLoadException) { hdlr.ListError(aSpan, 95); }
            catch (Exception x) { hdlr.AddError(x.Message, aSpan.Merge(mSpan)); throw; }
        }

        internal void AddRuleToList(RuleDesc rule)
        {
            //
            // Versions before 0.4.2.* had incorrect semantics
            // for the handling of inclusive start states.
            // Correct semantics for inclusive start states:
            // If a rule has no explicit start state(s) then it
            // should be added to *every* inclusive start state.
            //
            // For version 0.5.1+ the semantics follow those of
            // FLEX, which distinguishes between rules that are
            // *explicitly* attached to INITIAL, and those which
            // have an empty start state list.  Only those without
            // a start state list are added to inclusive states.
            //

            if (rule.list == null || rule.list.Count == 0)
            {
                StartState.initState.AddRule(rule);       // Add to initial state
                foreach (StartState inclS in inclStates)  // Add to inclusive states
                    inclS.AddRule(rule);
            }
            else if (rule.list[0].IsAll)
                AddToAllStates(rule);
            else
                foreach (StartState state in rule.list)
                    state.AddRule(rule);
        }

        internal LexSpan AtStart
        { get { LexSpan tmp = new LexSpan(1,1,1,1,0,0,scanner.Buffer); return tmp; } }

        /// <summary>
        /// NESTED CLASS
        /// Regular expression parser -- no error recovery attempted
        /// just throw an exception and abandon the whole pattern.
        /// This is a hand-written, recursive descent parser.
        /// </summary>
        internal sealed class ReParser
        {
            BitArray prStart;
            // Dictionary<string, PredicateLeaf> cats; // Allocated on demand

            const char NUL = '\0';
            int symCard;
            int index;              // index of the *next* character to be read
            char chr;               // the last char to be read.
            bool esc;               // the last character was backslash-escaped
            AAST parent;
            LexSpan span;
            string pat;

            /// <summary>
            /// Defines the character set special to regular expressions
            /// and the valid characters to start syntatic category "Primary"
            /// </summary>
            /// <param name="crd">host alphabet cardinality</param>
            void InitReParser() {
                prStart = new BitArray(symCard, true);
                prStart[(int)')'] = false;
                prStart[(int)'|'] = false;
                prStart[(int)'*'] = false;
                prStart[(int)'+'] = false;
                prStart[(int)'?'] = false;
                prStart[(int)'}'] = false;
                prStart[(int)'/'] = false;
                prStart[(int)')'] = false;
                prStart[(int)'$'] = false;
                prStart[(int)'/'] = false;                
                prStart[(int)'\0'] = false;
            }

            internal ReParser(string str, LexSpan spn, AAST parent) {
                if (parent.task.Unicode)
                    CharacterUtilities.SetUnicode();
                symCard = parent.task.HostSymCardinality;
                pat = str;
                span = spn;
                InitReParser();
                this.parent = parent;
            }

            internal RegExTree Parse()
            {
                try {
                    RegExTree tmp;
                    scan();
                    tmp = RegEx();
                    return tmp;
                } catch (RegExException x) {
                    x.ListError(parent.hdlr, this.span);
                    return null;
                }
                catch (StringInterpretException x)
                {
                    parent.hdlr.ListError(this.span, 99, x.Key);
                    return null;
                }
            }

            internal void scan() {
                int len = pat.Length;
                chr = (index == len ? NUL : pat[index++]);
                esc = (chr == '\\');
                if (esc) 
                    chr = (index == len ? NUL : pat[index++]);
            }

            /// <summary>
            /// Do lookahead one position in string buffer
            /// </summary>
            /// <returns>lookahead character or NUL if at end of string</returns>
            internal char peek() {
                return (index == pat.Length ? NUL : pat[index]);
            }

            internal bool isEofString() {
                // The EOF string must be exactly "<<EOF>>"
                return (pat.Length >= 7 && pat[0] == '<' && pat.Substring(0, 7).Equals("<<EOF>>"));
            }

            internal int GetInt()
            {
                int val = (int)chr - (int)'0';
                scan();
                while (Char.IsDigit(chr))
                {
                    checked { val = val * 10 + (int)chr - (int)'0'; }
                    scan();
                }
                return val;
            }

            static void Error(int num, int idx, int len, string str)
            { throw new RegExException(num, idx, len, str); }

            void Warn(int num, int idx, int len, string str)
            { parent.hdlr.ListError(span.FirstLineSubSpan(idx, len), num, str, '"'); }

            internal void checkAndScan(char ex)
            {
                if (chr == ex) 
                    scan(); 
                else 
                    Error(53, index-1, 1, "'" + ex + "'");
            }

            internal RegExTree RegEx()
            {
                if (isEofString())
                    return new Leaf(RegOp.eof);
                else
                {
                    RegExTree tmp = Expr();
                    if (chr != '\0')
                        Error(101, index-1, pat.Length - index + 1, null);
                    return tmp;
                }
            }

            internal RegExTree Expr()
            {
                RegExTree tmp;
                if (!esc && chr == '^')
                {
                    scan();
                    tmp = new Unary(RegOp.leftAnchor, Simple());
                }
                else
                    tmp = Simple();
                if (!esc && chr == '$')
                {
                    scan();
                    tmp = new Unary(RegOp.rightAnchor, tmp);
                }
                return tmp;
            }

            internal RegExTree Simple()
            {
                RegExTree tmp = Term();
                if (!esc && chr == '/')
                {
                    scan();
                    return new Binary(RegOp.context, tmp, Term());
                }
                //else if (!esc && chr == '$')
                //{
                //    scan();
                //    return new Unary(RegOp.rightAnchor, tmp);
                //}
                return tmp;
            }

            internal RegExTree Term()
            {
                RegExTree tmp = Factor();
                while (!esc && chr == '|')
                {
                    scan();
                    tmp = new Binary(RegOp.alt, tmp, Factor());
                }
                return tmp;
            }

            internal RegExTree Factor()
            {
                RegExTree tmp = Primary();
                while (prStart[(int)chr] || esc)
                    tmp = new Binary(RegOp.concat, tmp, Primary());
                return tmp;
            }

            internal RegExTree LitString()
            {
                int pos = index;
                int len;
                string str;
                scan();                 // get past '"'
                while (esc || (chr != '"' && chr != NUL))
                    scan();
                len = index - 1 - pos;
                checkAndScan('"');
                str = pat.Substring(pos, len);
                try
                {
                    str = CharacterUtilities.InterpretCharacterEscapes(str);
                }
                catch (RegExException x)
                {
                    // InterpretCharacterEscapes takes only a
                    // substring of "this.pat". RegExExceptions
                    // that are thrown will have an index value
                    // relative to this substring, so the index
                    // is transformed relative to "this.pat".
                    x.AdjustIndex(pos);
                    throw;
                }
                return new Leaf(str);
            }

            internal RegExTree Primary()
            {
                RegExTree tmp;
                Unary     pls;
                if (!esc && chr == '"')
                    tmp = LitString();
                else if (!esc && chr == '(')
                {
                    scan(); 
                    tmp = Term(); 
                    checkAndScan(')');
                }
                else 
                    tmp = Primitive();

                if (!esc && chr == '*')
                {
                    scan();
                    tmp = new Unary(RegOp.closure, tmp);
                }
                else if (!esc && chr == '+')
                {
                    pls = new Unary(RegOp.closure, tmp);
                    pls.minRep = 1;
                    scan();
                    tmp = pls;
                }
                else if (!esc && chr == '?')
                {
                    pls = new Unary(RegOp.finiteRep, tmp);
                    pls.minRep = 0;
                    pls.maxRep = 1;
                    scan();
                    tmp = pls;
                }
                else if (!esc && chr == '{' && Char.IsDigit(peek()))
                {
                    pls = new Unary(RegOp.finiteRep, tmp);
                    GetRepetitions(pls);
                    tmp = pls;
                }
                return tmp;
            }

            internal void GetRepetitions(Unary tree) 
            {
                scan();          // read past '{'
                tree.minRep = GetInt();
                if (!esc && chr == ',')
                {
                    scan();
                    if (Char.IsDigit(chr))
                        tree.maxRep = GetInt();
                    else
                        tree.op = RegOp.closure;
                }
                else
                    tree.maxRep = tree.minRep;
                checkAndScan('}');
            }

            int EscapedChar()
            {
                index--;
                return CharacterUtilities.EscapedChar(pat, ref index);
            }

            int CodePoint()
            {
                if (!Char.IsHighSurrogate(chr))
                    return (int)chr;
                index--;
                return CharacterUtilities.CodePoint(pat, ref index);
            }

            internal RegExTree Primitive()
            {
                RegExTree tmp;
                if (!esc && chr == '[')
                    tmp = CharClass();
                else if (!esc && chr == '{' && !Char.IsDigit(peek()))
                    tmp = UseLexCat();
                else if (!esc && chr == '.')
                {
                    Leaf leaf = new Leaf(RegOp.charClass);
                    leaf.rangeLit = new RangeLiteral(true);
                    scan();
                    leaf.rangeLit.list.Add(new CharRange('\n'));
                    tmp = leaf;
                }
                // Remaining cases are:
                //  1. escaped character (maybe beyond ffff limit)
                //  2. ordinary unicode character
                //  3. maybe a surrogate pair in future
                else if (esc)
                {
                    tmp = new Leaf(EscapedChar());
                    scan();
                }
                else
                {
                    tmp = new Leaf((int)chr);
                    scan();
                }
                return tmp;
            }

            internal RegExTree UseLexCat()
            {
                // Assert chr == '{'
                int start;
                string name;
                LexCategory cat;
                scan();                                     // read past '{'
                start = index - 1;
                while (chr != '}' && chr != NUL)
                    scan();
                name = pat.Substring(start, index - start - 1);
                checkAndScan('}');
                if (parent.lexCategories.TryGetValue(name, out cat))
                {
                    Leaf leaf = cat.regX as Leaf;
                    if (leaf != null && leaf.op == RegOp.charClass)
                        leaf.rangeLit.name = name;
                    return cat.regX;
                }
                else
                    Error(55, start, name.Length, name);
                return null;
            }

            internal RegExTree CharClass()
            {
                // Assert chr == '['
                // Need to build a new string taking into account char escapes
                Leaf leaf = new Leaf(RegOp.charClass);
                bool invert = false;
                scan();                           // read past '['
                if (!esc && chr == '^')
                {
                    invert = true;
                    scan();                       // read past '^'
                }
                leaf.rangeLit = new RangeLiteral(invert);
                // Special case of '-' at start, taken as ordinary class member.
                // This is correct for LEX specification, but is undocumented
                // behavior for FLEX. GPLEX gives a friendly warning, just in
                // case this is actually a typographical error.
                if (!esc && chr == '-')
                {
                    Warn(113, index - 1, 1, "-");
                    leaf.rangeLit.list.Add(new CharRange('-'));
                    scan();                       // read past -'
                }

                while (chr != NUL && (esc || chr != ']'))
                {
                    int lhCodePoint;
                    int startIx = index-1; // save starting index for error reporting
                    lhCodePoint = (esc ? EscapedChar() : CodePoint());
                    if (!esc && lhCodePoint == (int)'-')
                        Error(82, startIx, index - startIx, null);
                    //
                    // There are three possible elements here:
                    //  * a singleton character
                    //  * a character range
                    //  * a character category like [:IsLetter:]
                    //
                    if (chr == '[' && !esc && peek() == ':') // character category
                    {
                        Leaf rslt = GetCharCategory();
                        leaf.Merge(rslt);
                    }
                    else
                    {
                        scan();
                        if (!esc && chr == '-')             // character range
                        {
                            scan();
                            if (!esc && chr == ']')
                            {
                                // Special case of '-' at end, taken as ordinary class member.
                                // This is correct for LEX specification, but is undocumented
                                // behavior for FLEX. GPLEX gives a friendly warning, just in
                                // case this is actually a typographical error.
                                leaf.rangeLit.list.Add(new CharRange(lhCodePoint));
                                leaf.rangeLit.list.Add(new CharRange('-'));
                                //Error(81, idx, index - idx - 1);
                                Warn(114, startIx, index - startIx - 1, String.Format(
                                    CultureInfo.InvariantCulture, 
                                    "'{0}','{1}'", 
                                    CharacterUtilities.Map(lhCodePoint), 
                                    '-'));
                            }
                            else
                            {
                                int rhCodePoint = (esc ? EscapedChar() : CodePoint());
                                if (rhCodePoint < lhCodePoint)
                                    Error(54, startIx, index - startIx, null);
                                scan();
                                leaf.rangeLit.list.Add(new CharRange(lhCodePoint, rhCodePoint));
                            }
                        }
                        else                               // character singleton
                        {
                            leaf.rangeLit.list.Add(new CharRange(lhCodePoint));
                        }
                    }
                }
                checkAndScan(']');
                return leaf;
            }

            private Leaf GetCharCategory()
            {
                // Assert: chr == '[', next is ':'
                int start;
                string name;
                PredicateLeaf rslt;
                scan(); // read past '['
                scan(); // read past ':'
                start = index - 1;
                while (Char.IsLetter(chr)) // Need revision for any ident ...
                    scan();
                name = pat.Substring(start, index - start - 1);
                if (!GetCharCategory(name, out rslt))
                    Error(76, start, name.Length, name);
                checkAndScan(':');
                checkAndScan(']');
                return rslt;
            }

            private bool GetCharCategory(string name, out PredicateLeaf rslt)
            {
                // lazy allocation of dictionary
                if (parent.cats == null) 
                    parent.InitCharCats();
                bool found = parent.cats.TryGetValue(name, out rslt);
                // lazy population of element range lists
                if (found && rslt.rangeLit == null)
                    rslt.Populate(name, parent);
                return found;
            }  
        }
	}

    /// <summary>
    /// Objects of this class carry exception information
    /// out to the call of Parse() on the regular expression.
    /// These exceptions cannot escape beyond the enclosing
    /// call of Parse().
    /// </summary>
    [Serializable]
    [SuppressMessage("Microsoft.Design", "CA1064:ExceptionsShouldBePublic")]
    // Reason for FxCop message suppression -
    // This exception cannot escape from the local context
    internal class RegExException : Exception
    {
        int errNo;
        int index;
        int length;
        string text;

        internal RegExException(int errorNum, int stringIx, int count, string message)
        { errNo = errorNum; index = stringIx; length = count; text = message; }

        protected RegExException(SerializationInfo i, StreamingContext c) : base(i, c) { }

        internal RegExException AdjustIndex(int delta)
        { this.index += delta; return this; }

        internal void ListError(ErrorHandler handler, LexSpan span)
        {
            if (text == null)
                handler.ListError(span.FirstLineSubSpan(index, length), errNo);
            else
                handler.ListError(span.FirstLineSubSpan(index, length), errNo, text);
        }
    }

    internal sealed class StartState
    {
        static int next = -1;

        int ord;
        //bool isExcl;
        //bool isInit;
        bool isAll;
        bool isDummy;
        string name;
        internal List<RuleDesc> rules = new List<RuleDesc>();

        internal static StartState allState = new StartState("$ALL$", true);    // ord = -1
        internal static StartState initState = new StartState("INITIAL", false); // ord = 0;

        internal StartState(bool isDmy, string str)
        {
            isDummy = isDmy; name = str; ord = next++;
        }

        StartState(string str, bool isAll)
        {
            name = str; this.isAll = isAll; ord = next++ ;
        }

        internal string Name { get { return name; } }
        internal int Ord { get { return ord; } }
        internal bool IsAll { get { return isAll; } }
        internal bool IsDummy { get { return isDummy; } }

        internal void AddRule(RuleDesc rule)
        {
            rules.Add(rule);
        }
    }

    /// <summary>
    /// This class models the nested start-state scopes.
    /// </summary>
    internal sealed class StartStateScope
    {
        private Stack<List<StartState>> stack;

        internal StartStateScope()
        {
            stack = new Stack<List<StartState>>();
            stack.Push(new List<StartState>());
        }

        internal List<StartState> Current
        {
            get { return stack.Peek(); }
        }

#if STATE_DIAGNOSTICS
        /// <summary>
        /// Diagnostic method for debugging GPLEX
        /// </summary>
        internal void DumpCurrent()
        {
            Console.Write("Current start states: ");
            foreach (StartState elem in Current)
            {
                Console.Write("{0},", elem.Name);
            }
            Console.WriteLine();
        }
#endif

        internal void EnterScope(List<StartState> list)
        {
            //  There are a couple of tricky cases here.
            //  Neither of them are sensible, but both are
            //  probably legal:
            //  (1)   <*>{
            //            <FOO>{ ...
            //  Active list of start states should be "all states"
            //
            //  (2)   <FOO>{
            //            <*>{ ...
            //  Active list of start states should be "all states"
            //
            List<StartState> newTop = null;
            List<StartState> current = this.Current;
            if (list.Contains(StartState.allState))
                newTop = list;
            else if (current.Contains(StartState.allState))
                newTop = current;
            else
            {
                newTop = new List<StartState>(this.Current);
                foreach (StartState elem in list)
                {
                    if (!newTop.Contains(elem))
                        newTop.Add(elem);
                }
            }
            stack.Push(newTop);
        }

        internal void ExitScope()
        {
            if (stack.Count > 1)
                stack.Pop();
        }

        internal void ClearScope()
        {
            if (stack.Count != 1)
            {
                stack = new Stack<List<StartState>>();
                stack.Push(new List<StartState>());
            }
        }
    }

    internal sealed class RuleDesc
    {
        static int next = 1;
        string pattern;       // Span of pattern reg-exp
        int minPatternLength;
        internal LexSpan pSpan;
        internal int ord;
        RegExTree reAST;
        internal LexSpan aSpan; // Span of lexical action
        internal bool isBarAction;
        //internal bool isRightAnchored;
        internal bool isPredDummyRule;
        internal List<StartState> list;

        /// <summary>
        /// How many times this rule is used
        /// in the construction of the DFSA. 
        /// May decrease due to replacement.
        /// </summary>
        internal int useCount;
        /// <summary>
        /// If rule is replaced, the new rule
        /// that replaced this one.
        /// </summary>
        internal RuleDesc replacedBy;

        internal string Pattern { get { return pattern; } }

        /// <summary>
        /// A pattern is a loop risk iff:
        /// (1) it can match the empty string,
        /// (2) it has non-zero right context.
        /// Condition (1) alone will not cause a loop, since
        /// any non-zero length match will take precedence.
        /// </summary>
        internal bool IsLoopRisk { 
            get 
            {
                return
                    pSpan != null &&
                    reAST != null &&
                    minPatternLength == 0 &&
                    reAST.HasRightContext;
            } 
        }

        private RuleDesc() { }

        internal RuleDesc(LexSpan loc, LexSpan act, List<StartState> aList, bool bar)
        {
            pSpan = loc;
            aSpan = act;
            pattern = pSpan.buffer.GetString(pSpan.startIndex, pSpan.endIndex);
            isBarAction = bar;
            list = aList;
            ord = next++;
        }

        internal static RuleDesc MkDummyRuleDesc(LexCategory cat, AAST aast)
        {
            RuleDesc result = new RuleDesc();
            result.pSpan = null;
            result.aSpan = aast.AtStart;
            result.isBarAction = false;
            result.isPredDummyRule = true;
            result.pattern = String.Format(CultureInfo.InvariantCulture, "{{{0}}}", cat.Name);
            result.list = new List<StartState>();
            result.ParseRE(aast);
            result.list.Add(aast.StartStateValue(cat.PredDummyName));
            return result;
        }

        internal RegExTree Tree { get { return reAST; } }
        internal bool hasAction { get { return aSpan.IsInitialized; } }
        // internal void Dump() { Console.WriteLine(pattern); }

        internal void ParseRE(AAST aast)
        {
            reAST = new AAST.ReParser(pattern, pSpan, aast).Parse();
            SemanticCheck(aast);
        }

        /// <summary>
        /// This is the place to perform any semantic checks on the 
        /// trees corresponding to a rule of the LEX grammar,
        /// during a recursive traversal of the tree.  It is hard
        /// to do these on the fly during AST construction, because
        /// of the tree-grafting that happens for lexical categories.
        /// 
        /// First check is that '^' and '$' can only appear 
        /// (logically) at the ends of the pattern.
        /// Later need to check ban on multiple right contexts ...
        /// </summary>
        /// <param name="aast"></param>
        void SemanticCheck(AAST aast)
        {
            RegExTree tree = reAST;
            if (tree != null && tree.op == RegOp.leftAnchor) tree = ((Unary)tree).kid;
            if (tree != null && tree.op == RegOp.rightAnchor)
            {
                tree = ((Unary)tree).kid;
                if (tree.op == RegOp.context)
                    aast.hdlr.ListError(pSpan, 100);
            }
            Check(aast, tree);
            if (tree != null) 
                minPatternLength = tree.minimumLength();
        }

        void Check(AAST aast, RegExTree tree)
        {
            Binary bnryTree;
            Unary unryTree;

            if (tree == null) return;
            switch (tree.op)
            {
                case RegOp.charClass:
                case RegOp.primitive:
                case RegOp.litStr:
                case RegOp.eof:
                    break;
                case RegOp.context:
                case RegOp.concat:
                case RegOp.alt:
                    bnryTree = (Binary)tree;
                    Check(aast, bnryTree.lKid);
                    Check(aast, bnryTree.rKid);
                    if (tree.op == RegOp.context && 
                        bnryTree.lKid.contextLength() == 0 &&
                        bnryTree.rKid.contextLength() == 0) aast.hdlr.ListError(pSpan, 75);
                    break;
                case RegOp.closure:
                case RegOp.finiteRep:
                    unryTree = (Unary)tree;
                    Check(aast, unryTree.kid);
                    break;
                case RegOp.leftAnchor:
                case RegOp.rightAnchor:
                    aast.hdlr.ListError(pSpan, 69);
                    break;
            }
        }
    }

    internal sealed class LexCategory
    {
        string name;
        string verb;
        LexSpan vrbSpan;
        bool hasPred;
        internal RegExTree regX;

        internal LexCategory(string nam, string vrb, LexSpan spn)
        {
            vrbSpan = spn;
            verb = vrb;
            name = nam;
        }

        internal bool HasPredicate { 
            get { return hasPred; }  
            set { hasPred = value; }
        }

        internal string Name
        { get { return name; } }

        internal string PredDummyName
        { get { return "PRED_" + name + "_DUMMY"; } }

        internal void ParseRE(AAST aast)
        { regX = new AAST.ReParser(verb, vrbSpan, aast).Parse(); }
    }

    internal sealed class RuleBuffer
    {
        List<LexSpan> locs = new List<LexSpan>();
        int fRuleLine, lRuleLine;  // First line of rules, last line of rules.

        internal int FLine { get { return fRuleLine; } set { fRuleLine = value; } }
        internal int LLine { get { return lRuleLine; } set { lRuleLine = value; } }

        internal void AddSpan(LexSpan l) { locs.Add(l); }

        /// <summary>
        /// This method detects the presence of code *between* rules. Such code has
        /// no unambiguous meaning, and is skipped, with a warning message.
        /// </summary>
        /// <param name="aast"></param>
        internal void FinalizeCode(AAST aast)
        {
            for (int i = 0; i < locs.Count; i++)
            {
                LexSpan loc = locs[i];

                if (loc.startLine < FLine) 
                    aast.AddCodeSpan(AAST.Destination.scanProlog, loc);
                else if (loc.startLine > LLine) 
                    aast.AddCodeSpan(AAST.Destination.scanEpilog, loc);
                else // code is between rules
                    aast.hdlr.ListError(loc, 110);
            }
        }
    }

    #region AST for Regular Expressions

    internal enum RegOp
    {
        eof,
        context,
        litStr,
        primitive,
        concat,
        alt,
        closure,
        finiteRep,
        charClass,
        leftAnchor,
        rightAnchor
    }

    internal abstract class RegExDFS
    {
        internal abstract void Op(RegExTree tree);
    }
     
    /// <summary>
    /// Abstract class for AST representing regular expressions.
    /// Concrete subclasses correspond to --- 
    /// binary trees (context, alternation and concatenation)
    /// unary trees (closure, finite repetition and anchored patterns)
    /// leaf nodes (chars, char classes, literal strings and the eof marker)
    /// </summary>
    internal abstract class RegExTree
    {
        internal RegOp op;
        internal RegExTree(RegOp op) { this.op = op; }

        /// <summary>
        /// This is a helper to compute the length of strings
        /// recognized by a regular expression.  This is important
        /// because the right context operator "R1/R2" is efficiently 
        /// implemented if either R1 or R2 produce fixed length strings.
        /// </summary>
        /// <returns>0 if length is variable, otherwise length</returns>
        internal abstract int contextLength();

        /// <summary>
        /// This is a helper to compute the minimum length of
        /// strings recognized by a regular expression.  It is the
        /// minimum consumption of input by a pattern.
        /// </summary>
        /// <returns>Minimum length of pattern</returns>
        internal abstract int minimumLength();

        /// <summary>
        /// This is the navigation method for running the visitor
        /// over the tree in a depth-first-search visit order.
        /// </summary>
        /// <param name="visitor">visitor.Op(this) is called on each node</param>
        internal abstract void Visit(RegExDFS visitor);

        internal virtual bool HasRightContext { get { return false; } }
    }

    internal class Leaf : RegExTree
    {   // charClass, EOF, litStr, primitive

        internal int chVal;     // in case of primitive char
        internal string str;
        internal RangeLiteral rangeLit;

        internal Leaf(string s) : base(RegOp.litStr) { str = CharacterUtilities.InterpretCharacterEscapes(s); }
        internal Leaf(int code) : base(RegOp.primitive) { chVal = code; }
        internal Leaf(RegOp op) : base(op) {}


        internal override int contextLength()
        {
            return (op == RegOp.litStr ? str.Length : 1);
        }

        internal override int minimumLength() { return contextLength(); }

        internal override void Visit(RegExDFS visitor) { visitor.Op(this); }

        internal void Merge(Leaf addend)
        {
            foreach (CharRange rng in addend.rangeLit.list.Ranges)
                this.rangeLit.list.Add(rng);
        }
    }

    internal sealed class PredicateLeaf : Leaf
    {
        CharTest Test;

        internal PredicateLeaf() : base(RegOp.charClass) { }

        internal PredicateLeaf(CharTest test)
            : base(RegOp.charClass) { this.Test = test; }

        internal static CharTest MkCharTest(CharPredicate cPred, CodePointPredicate cpPred)
        {
            return delegate(int ord)
            {
                // Use the Char function for the BMP
                if (ord <= (int)Char.MaxValue)
                    return cPred((char)ord);
                else
                    return cpPred(Char.ConvertFromUtf32(ord), 0);
            };
        }

        /// <summary>
        /// This method constructs a RangeLiteral holding
        /// all of the codepoints from all planes for which
        /// the Test delegate returns true.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="aast"></param>
        /// <param name="max"></param>
        internal void Populate(string name, AAST aast)
        {
            DateTime begin = DateTime.Now;

            int max = aast.Task.TargetSymCardinality;

            this.rangeLit = new RangeLiteral(false);
            //
            // Run the delegate over all the values 
            // between '\0' and (max-1).  Find contiguous
            // true values and add to the CharRange list.
            //
            int j = 0;
            int codepage = aast.CodePage;
            if (max > 256 || 
                codepage == Automaton.TaskState.rawCP ||
                codepage == Automaton.TaskState.guessCP)
            {
                if (max <= 256 && codepage == Automaton.TaskState.guessCP)
                    aast.hdlr.ListError(aast.AtStart, 93);
                // We are generating a set of numeric code points with
                // the named property.  No interpretation is needed, either
                // (1) because this is for a unicode scanner that has
                //     already decoded its input element stream, OR
                // (2) the user has commanded /codepoint:raw to indicate
                //     that no interpretation is to be used.
                //
                while (j < max)
                {
                    int start;
                    while (j < max && !Test(j))
                        j++;
                    if (j == max)
                        break;
                    start = j;
                    while (j < max && Test(j))
                        j++;
                    this.rangeLit.list.Add(new CharRange(start, (j - 1)));
                }
            }
            else 
            {
                // We are generating a set of byte values from the
                // 0x00 to 0xFF "alphabet" that correspond to unicode
                // characters with the named property.  The meaning of
                // "corresponds" is defined by the nominated codepage.
                //
                // Check codepage for single byte property.
                //
                Encoding enc = Encoding.GetEncoding(codepage);
                Decoder decoder = enc.GetDecoder();
                if (!enc.IsSingleByte)
                    aast.hdlr.ListError(aast.AtStart, 92);
                //
                // Construct character map for bytes.
                //
                int bNum, cNum;
                bool done;
                char[] cArray = new char[256];
                byte[] bArray = new byte[256];
                for (int b = 0; b < 256; b++)
                {
                    bArray[b] = (byte)b;
                    cArray[b] = '?';
                }
                decoder.Convert(bArray, 0, 256, cArray, 0, 256, true, out bNum, out cNum, out done);
                //
                // Now construct the CharRange literal
                //
                while (j < max)
                {
                    int start;
                    while (j < max && !Test(cArray[j]))
                        j++;
                    if (j == max)
                        break;
                    start = j;
                    while (j < max && Test(cArray[j]))
                        j++;
                    this.rangeLit.list.Add(new CharRange(start, (j - 1)));
                }
            }
            if (aast.IsVerbose)
            {
                Console.WriteLine("GPLEX: Generating [:{0}:], {1}", name, Gplex.Automaton.TaskState.ElapsedTime(begin));
                //Console.WriteLine("{0} Lex Representation for codepage {1}", name, codepage);
                //Console.WriteLine(rangeLit.list.LexRepresentation());
            }
        }
    }

    internal sealed class Unary : RegExTree
    { // leftAnchor, rightAnchor, finiteRep, closure
        internal RegExTree kid;
        internal int minRep;         // min repetitions for closure/finiteRep
        internal int maxRep;         // max repetitions for finiteRep.
        internal Unary(RegOp op, RegExTree l) : base(op) { kid = l;  } 

        internal override int contextLength()
        {
            switch (op)
            {
                case RegOp.closure: return 0;
                case RegOp.finiteRep: return (minRep == maxRep ? kid.contextLength() * minRep : 0);
                case RegOp.leftAnchor: return kid.contextLength();
                case RegOp.rightAnchor: return kid.contextLength();
                default: throw new GplexInternalException("unknown unary RegOp");
            }
        }

        internal override int minimumLength()
        {
            switch (op)
            {
                case RegOp.closure:
                case RegOp.finiteRep: return kid.minimumLength() * minRep;
                case RegOp.leftAnchor:
                case RegOp.rightAnchor: return kid.minimumLength();
                default: throw new GplexInternalException("unknown unary RegOp");
            }
        }

        internal override void Visit(RegExDFS visitor)
        {
            visitor.Op(this);
            kid.Visit(visitor);
        }

        internal override bool HasRightContext
        {
            get { return op == RegOp.leftAnchor && kid.HasRightContext; }
        }
    }

    internal sealed class Binary : RegExTree
    {
        internal RegExTree lKid, rKid;
        internal Binary(RegOp op, RegExTree l, RegExTree r) : base(op) { lKid = l; rKid = r; } 

        internal override int contextLength()
        {
            if (op == RegOp.context) throw new StringInterpretException("multiple context operators");
            else
            {
                int lLen = lKid.contextLength();
                int rLen = rKid.contextLength();
                if (lLen <= 0 || rLen <= 0) return 0;
                else if (op == RegOp.concat) return lLen + rLen;
                else if (lLen == rLen) return lLen;
                else return 0;
            }
        }

        internal override int minimumLength()
        {
            switch (op)
            {
                case RegOp.concat: return lKid.minimumLength() + rKid.minimumLength();
                case RegOp.context: return lKid.minimumLength();
                case RegOp.alt:
                    {
                        int lLen = lKid.minimumLength();
                        int rLen = rKid.minimumLength();
                        return (lLen <= rLen ? lLen : rLen);
                    }
                default: throw new GplexInternalException("Bad binary RegOp");
            }
        }

        internal override bool HasRightContext
        {
            get { return op == RegOp.context; }
        }

        internal override void Visit(RegExDFS visitor)
        {
            visitor.Op(this);
            lKid.Visit(visitor);
            rKid.Visit(visitor);
        }
    }
    #endregion
}