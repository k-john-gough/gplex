// Gardens Point Scanner Generator
// Copyright (c) K John Gough, QUT 2006-2008
// (see accompanying GPLEXcopyright.rtf)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using QUT.Gplex.Automaton;

namespace QUT.Gplex.Parser
{
    /// <summary>
    /// Partition is the class that represents a 
    /// partitioning of the character set into 
    /// equivalence classes for a particular set
    /// of literals from a given LEX specification.
    /// The literals are of two kinds: singleton
    /// characters and character sets [...].
    /// A partition object is initialized with 
    /// a single range from CharMin to CharMax,
    /// this is refined by the Refine method.
    /// The invariants of the partition are:
    /// (1) the character sets denoted by 
    /// the partition elements are disjoint,
    /// and together cover the character range.
    /// (2) for every processed literal L, and
    /// every partition element E, either every
    /// character in E is in L, or none is.
    /// </summary>
    internal class Partition
    {
        internal const int CutOff = 128; // Shortest run to consider
        internal const int PageSize = 256; // Pagesize for two-level map

        internal List<PartitionElement> elements = new List<PartitionElement>();

        internal List<MapRun> mapRuns;
        internal List<MapRun> runsInBMP = new List<MapRun>();
        internal List<MapRun> runsInNonBMPs = new List<MapRun>();
        internal TaskState myTask;

        internal List<MapRun> pages;

        int[] iMap;
        CharClassMap tMap;

        /// <summary>
        /// Create a new partition
        /// </summary>
        /// <param name="symCard">The symbol alphabet cardinality</param>
        internal Partition(int symCard, TaskState task) {
            this.myTask = task;
            CharRange.Init(symCard);
            PartitionElement.Reset();

            elements.Add(PartitionElement.AllChars());
        }

        /// <summary>
        /// The mapping function from character
        /// ordinal to equivalence class ordinal.
        /// </summary>
        // internal int this[char ch] { get { return cMap[(int)ch]; } }
        internal int this[int chVal] { get { return tMap[chVal]; } }

        /// <summary>
        /// A projection of the inverse map from class ordinal
        /// back to character ordinal. iMap returns an example 
        /// character, used for diagnostic information only.
        /// </summary>
        internal int InvMap(int ch) { return iMap[ch]; }

        /// <summary>
        /// The number of equivalence classes.
        /// </summary>
        internal int Length { get { return elements.Count; } }

        internal void FindClasses(AAST aast)
        {
            Accumulator visitor = new Accumulator(this);
            foreach (RuleDesc rule in aast.ruleList)
            {
                RegExTree reTree = rule.Tree;
                reTree.Visit(visitor);
            }
        }

        /// <summary>
        /// Fix the partition, and generate the forward and
        /// inverse mappings "char : equivalence class"
        /// </summary>
        internal void FixMap()
        {
            // Create the inverse map from partition element
            // ordinal to (an example) character.
            iMap = new int[elements.Count];
            foreach (PartitionElement pElem in elements)
            {
                if (pElem.list.Ranges.Count > 0)
                    iMap[pElem.ord] = pElem.list.Ranges[0].minChr;
            }

            tMap = new CharClassMap(this);
            FindMapRuns();
        }

        /// <summary>
        /// Refine the current partition with respect 
        /// to the given range literal "lit".
        /// </summary>
        /// <param name="lit">The Range Literal</param>
        internal void Refine(RangeLiteral lit)
        {
            int idx;
            int max = elements.Count; // because this varies inside the loop
            //
            // For each of the *current* elements of the partition do:
            //
            for (idx = 0; idx < max; idx++)
            {
                PartitionElement elem = elements[idx];
                RangeList intersection = lit.list.AND(elem.list);
                // 
                // There are four cases here:
                // (1) No intersection of lit and elem ... do nothing
                // (2) Literal properly contains the partition element ...
                //     Add this element to the equivClasses list of this lit.
                //     Add this lit to the list of literals dependent on "elem".
                //     The intersection of other parts of the literal with other
                //     elements will be processed by other iterations of the loop.
                // (3) Literal is properly contained in the partition element ...
                //     Split the element into two: a new element containing the
                //     intersection, and an updated "elem" with the intersection
                //     subtracted. All literals dependent on the old element are
                //     now dependent on the new element and (the new version of)
                //     this element. The literal cannot overlap with any other 
                //     element, so the iteration can be terminated.
                // (4) Literal partially overlaps the partition element ...
                //     Split the element as for case 2.  Overlaps of the rest
                //     of the literal with other elements will be dealt with by
                //     other iterations of the loop. 
                //
                if (!intersection.IsEmpty) // not empty intersection
                {
                    // Test if elem is properly contained in lit
                    // If so, intersection == elem ...
                    if (intersection.EQU(elem.list))
                    {
                        elem.literals.Add(lit);
                        lit.equivClasses.Add(elem.ord);
                    }
                    else
                    {
                        PartitionElement newElem =
                            new PartitionElement(intersection.Ranges, false);
                        elements.Add(newElem);
                        lit.equivClasses.Add(newElem.ord);
                        newElem.literals.Add(lit);
                        //
                        //  We are about to split elem.
                        //  All literals that include elem
                        //  must now also include newElem
                        //
                        foreach (RangeLiteral rngLit in elem.literals)
                        {
                            rngLit.equivClasses.Add(newElem.ord);
                            newElem.literals.Add(rngLit);
                        }
                        elem.list = elem.list.SUB(intersection);
                        //
                        // Test if lit is a subset of elem
                        // If so, intersection == lit and we can
                        // assert that no other loop iteration has
                        // a non-empty intersection with this lit.
                        //
                        if (intersection.EQU(lit.list))
                            return;
                    }
                }
            }
        }

        internal void FindMapRuns()
        {
            List<MapRun> result = new List<MapRun>();
            int cardinality = CharRange.SymCard;
            int start = 0;                       // Start of the run
            int finish = 0;
            // Get first element and add to List
            tMap.GetEnclosingRange(start, out start, out finish);
            result.Add(new MapRun(start, finish));
            start = finish + 1;
            // Now get all the rest ...
            while (start < cardinality)
            {
                tMap.GetEnclosingRange(start, out start, out finish);
                MapRun lastRun = result[result.Count - 1];
                int length = finish - start + 1;
                if (length >= Partition.CutOff || lastRun.tag == MapRun.TagType.longRun)
                    result.Add(new MapRun(start, finish));
                else
                    lastRun.Merge(start, finish);
                start = finish + 1;
            }
            int total = 0;
            foreach (MapRun run in result)
            {
                if (run.tag == MapRun.TagType.mixedValues)
                    total += run.Length;
                if (run.range.maxChr <= Char.MaxValue) // ==> run is BMP
                    runsInBMP.Add(run);
                else if (run.range.minChr > Char.MaxValue) // ==> not in BMP
                    runsInNonBMPs.Add(run);
                else
                {
                    MapRun lowPart = new MapRun(run.range.minChr, Char.MaxValue);
                    MapRun highPart = new MapRun(Char.MaxValue + 1, run.range.maxChr);
                    if (run.range.minChr != Char.MaxValue)
                        lowPart.tag = run.tag;
                    if (Char.MaxValue + 1 != run.range.maxChr)
                        highPart.tag = run.tag;
                    runsInBMP.Add(lowPart);
                    runsInNonBMPs.Add(highPart);
                }
            }
            this.mapRuns = result;
        }

        internal void ComputePages()
        {
            List<MapRun> result = new List<MapRun>();
            // int cardinality = CharRange.SymCard;
            int cardinality = Char.MaxValue + 1;
            int stepSize = PageSize;
            int current = 0;                       // Start of the run
            while (current < cardinality)
            {
                int start, finish;
                int limit = current + stepSize - 1;
                MapRun thisRun;
                tMap.GetEnclosingRange(current, out start, out finish);
                if (finish > limit) finish = limit;
                thisRun = new MapRun(current, finish, tMap[current]);
                result.Add(thisRun);
                current = finish + 1;
                while (current <= limit)
                {
                    tMap.GetEnclosingRange(current, out start, out finish);
                    if (finish > limit) finish = limit;
                    thisRun.Merge(current, finish, tMap[current]);
                    current = finish + 1;
                }
            }
            this.pages = result;
        }

        /// <summary>
        /// Compares two map runs, but only for the cheap case
        /// of maps which are NOT mixed values.
        /// </summary>
        /// <param name="lRun">The LHS run</param>
        /// <param name="rRun">The RHS run</param>
        /// <returns>True if equal</returns>
        internal bool EqualMaps(MapRun lRun, MapRun rRun)
        {
            return
                (lRun.tag != MapRun.TagType.mixedValues &&
                rRun.tag != MapRun.TagType.mixedValues &&
                tMap[lRun.range.minChr] == tMap[rRun.range.minChr] &&
                tMap[lRun.range.maxChr] == tMap[rRun.range.maxChr]);
        }
    }


    /// <summary>
    /// This is the visitor pattern that extracts
    /// ranges from the leaves of the Regular Expressions.
    /// The RegExDFS base class provides the traversal
    /// code, while this extension provides the "Op" method.
    /// </summary>
    internal class Accumulator : RegExDFS
    {
        Partition partition;

        internal Accumulator(Partition part) { this.partition = part; }

        // Refine based on a single, isolated character.
        // The singleton is transformed into a RangeLiteral with just
        // one element in its RangeList, with a one-length range. 
        private void DoSingleton(int ch)
        {
#if DEBUG
            char c = (char)ch; // For readability
#endif
            if (this.partition.myTask.CaseAgnostic)
                partition.Refine(RangeLiteral.NewCaseAgnosticPair(ch));
            else 
                partition.Refine(new RangeLiteral(ch));
        }

        private void DoLiteral(RangeLiteral lit)
        {
            if (lit.lastPart != this.partition)
            {
                lit.lastPart = this.partition;
                if (this.partition.myTask.CaseAgnostic)
                    lit.list = lit.list.MakeCaseAgnosticList();
                lit.list.Canonicalize();
                partition.Refine(lit);
            }
        }

        internal override void Op(RegExTree tree)
        {
            Leaf leaf = tree as Leaf;
            if (leaf != null)
            {
                switch (leaf.op)
                {
                    case RegOp.primitive:
                        DoSingleton(leaf.chVal);
                        break;
                    case RegOp.litStr:
                        for (int index = 0; ; )
                        {
                            // Use CodePoint in case string has surrogate pairs.
                            int code = CharacterUtilities.CodePoint(leaf.str, ref index);
                            if (code == -1)
                                break;
                            else
                                DoSingleton(code);
                        } 
                        break;
                    case RegOp.charClass:
                        DoLiteral(leaf.rangeLit);
                        break;
                    case RegOp.eof: // no action required
                        break;
                    default:
                        throw new GplexInternalException("Unknown RegOp");
                }
            }
            else if (tree.op == RegOp.rightAnchor)
                DoLiteral(RangeLiteral.RightAnchors);
        }
    }

    /// <summary>
    /// Represents a set of characters as a
    /// list of character range objects.
    /// </summary>
    internal class RangeList
    {
        // Notes:
        // This is a sparse representation of the character set. The
        // operations that are supported in primitive code are AND,
        // the inversion of the underlying List<CharRange>, and
        // value equality EQU. This is functionally complete, over
        // the Boolean operations. The set difference SUB is packaged
        // as AND (inverse of rh operand).

        /// <summary>
        /// Asserts that the list has been canonicalized, that is
        /// (1) the ranges are in sorted order of minChr
        /// (2) there is no overlap of ranges
        /// (3) contiguous ranges have been merged
        /// (3) invert is false.
        /// The set operations AND, SUB, EQU rely on this property!
        /// </summary>
        private bool isCanonical = true;
        private bool isAgnostic = false;
        private bool invert;

        private List<CharRange> ranges;
        internal List<CharRange> Ranges { get { return ranges; } }

        /// <summary>
        /// Construct a new RangeList with the given
        /// list of ranges.
        /// </summary>
        /// <param name="ranges">the list of ranges</param>
        /// <param name="invert">if true, means the inverse of the list</param>
        internal RangeList(List<CharRange> ranges, bool invert) {
            this.invert = invert;
            this.ranges = ranges;
        }

        /// <summary>
        /// Construct an empty list, initialized with
        /// the invert flag specified.
        /// </summary>
        /// <param name="invert"></param>
        internal RangeList(bool invert) {
            this.invert = invert;
            ranges = new List<CharRange>(); 
        }

        internal bool IsEmpty { get { return ranges.Count == 0; } }
        internal bool IsInverted { get { return invert; } }

        internal void Add(CharRange rng)
        {
            ranges.Add(rng);  // AddToRange
            isCanonical = !invert && ranges.Count == 1;
        }

        /// <summary>
        /// Return a new RangeList that is the intersection of
        /// "this" and rhOp.  Neither operand is mutated.
        /// </summary>
        /// <param name="rhOp"></param>
        /// <returns></returns>
        internal RangeList AND(RangeList rhOp)
        {
            if (!isCanonical || !rhOp.isCanonical) 
                throw new GplexInternalException("RangeList non canonicalized");
            if (this.ranges.Count == 0 || rhOp.ranges.Count == 0)
                return new RangeList(false); // return empty RangeList

            int thisIx;
            int rhOpIx = 0;
            int thisNm = this.ranges.Count;
            int rhOpNm = rhOp.ranges.Count;
            List<CharRange> newList = new List<CharRange>();
            RangeList result = new RangeList(newList, false);
            CharRange rhOpElem = rhOp.ranges[rhOpIx++];
            for (thisIx = 0; thisIx < thisNm; thisIx++)
            {
                CharRange thisElem = this.ranges[thisIx];
                // Attempt to find an overlapping element.
                // If necessary fetch new elements from rhOp
                // until maxChr of the new element is greater
                // than minChr of the current thisElem.
                while (rhOpElem.maxChr < thisElem.minChr)
                    if (rhOpIx < rhOpNm)
                        rhOpElem = rhOp.ranges[rhOpIx++];
                    else
                        return result;
                // It is possible that the rhOpElem is entirely beyond thisElem
                // It is also possible that rhOpElem and several following 
                // elements are all overlapping with thisElem.
                while (rhOpElem.minChr <= thisElem.maxChr)
                {
                    // process overlap
                    newList.Add(new CharRange(
                        (thisElem.minChr < rhOpElem.minChr ? rhOpElem.minChr : thisElem.minChr),
                        (thisElem.maxChr < rhOpElem.maxChr ? thisElem.maxChr : rhOpElem.maxChr)));
                    // If rhOpElem extends beyond thisElem.maxChr it is possible that
                    // it will overlap with the next thisElem, so do not advance rhOpIx.
                    if (rhOpElem.maxChr > thisElem.maxChr)
                        break;
                    else if (rhOpIx == rhOpNm)
                        return result;
                    else 
                        rhOpElem = rhOp.ranges[rhOpIx++];
                }
            }
            return result;
        }

        /// <summary>
        /// Return a list of char ranges that represents
        /// the inverse of the set represented by "this".
        /// The RangeList must be sorted but not necessarily
        /// completely canonicalized.
        /// </summary>
        /// <returns></returns>
        internal List<CharRange> InvertedList()
        {
            int index = 0;
            List<CharRange> result = new List<CharRange>();
            foreach (CharRange range in this.ranges)
            {
                if (range.minChr > index)
                    result.Add(new CharRange(index, (range.minChr - 1)));
                index = range.maxChr + 1;
            }
            if (index < CharRange.SymCard)
                result.Add(new CharRange(index, (CharRange.SymCard - 1)));
            return result;
        }

        /// <summary>
        /// Return the set difference of "this" and rhOp
        /// </summary>
        /// <param name="rhOp"></param>
        /// <returns></returns>
        internal RangeList SUB(RangeList rhOp)
        {
            if (!isCanonical || !rhOp.isCanonical) 
                throw new GplexInternalException("RangeList not canonicalized");
            if (this.ranges.Count == 0)
                return new RangeList(false);
            else if (rhOp.ranges.Count == 0)
                return this;
            return this.AND(new RangeList(rhOp.InvertedList(), false)); 
        }

        /// <summary>
        /// Check value equality for "this" and rhOp.
        /// </summary>
        /// <param name="rhOp"></param>
        /// <returns></returns>
        internal bool EQU(RangeList rhOp)
        {
            if (!isCanonical || !rhOp.isCanonical)
                throw new GplexInternalException("RangeList not canonicalized");
            if (this == rhOp)
                return true;
            else if (this.ranges.Count != rhOp.ranges.Count)
                return false;
            else
            {
                for (int i = 0; i < this.ranges.Count; i++)
                    if (rhOp.ranges[i].CompareTo(ranges[i]) != 0) 
                        return false;
                return true;
            }
        }

        /// <summary>
        /// Canonicalize the set. This may mutate
        /// both this.ranges and the invert flag.
        /// </summary>
        internal void Canonicalize()
        {
            if (!invert && this.ranges.Count <= 1 || this.isCanonical)
                return; // Empty, singleton and upper/lower pair RangeLists are trivially canonical
            // Process non-empty lists.
            int listIx = 0;
            this.ranges.Sort();
            List<CharRange> newList = new List<CharRange>();
            CharRange currentRange = ranges[listIx++];
            while (listIx < ranges.Count)
            {
                CharRange nextRange = ranges[listIx++];
                if (nextRange.minChr > currentRange.maxChr + 1) // Merge contiguous ranges
                {
                    newList.Add(currentRange);
                    currentRange = nextRange;
                }
                else if
                    (nextRange.minChr <= (currentRange.maxChr + 1) &&
                     nextRange.maxChr >= currentRange.maxChr)
                {
                    currentRange = new CharRange(currentRange.minChr, nextRange.maxChr);
                }
                // Else skip ...
            }
            newList.Add(currentRange);
            this.ranges = newList;
            if (this.invert)
            {
                this.invert = false;
                this.ranges = this.InvertedList();
            }
            isCanonical = true;
        }

        /// <summary>
        /// Returns a new RangeList which is case-agnostic.
        /// The returned list will, in general, be seriously non-canonical.
        /// </summary>
        /// <returns>New case-insensitive list</returns>
        internal RangeList MakeCaseAgnosticList() {
            if (isAgnostic) return this; // Function is idempotent. Do not repeat.

            if (!isCanonical) this.Canonicalize();
            List<CharRange> agnosticList = new List<CharRange>();
            foreach (CharRange range in this.ranges) {
                for (int ch = range.minChr; ch <= range.maxChr; ch++) {
                    if (ch < char.MaxValue) {
                        char c = (char)ch;
                        char lo = char.ToLower(c);
                        char hi = char.ToUpper(c);
                        if (lo == hi)
                            agnosticList.Add(new CharRange(c));
                        else {
                            agnosticList.Add(new CharRange(lo));
                            agnosticList.Add(new CharRange(hi));
                        }
                    }
                    else
                        agnosticList.Add(new CharRange(ch));
                }
            }
            RangeList result = new RangeList(agnosticList, false);
            result.isCanonical = false;
            result.isAgnostic = true;
            return result;
        }

#if PARTITION_DIAGNOSTICS
        internal string LexRepresentation()
        {
            StringBuilder rslt = new StringBuilder();
            rslt.Append('[');
            if (invert)
                rslt.Append('^');
            if (!isCanonical)
                Canonicalize();
            foreach (CharRange range in ranges)
            {
                if (range.minChr == range.maxChr)
                    rslt.Append(CharacterUtilities.MapForCharSet(range.minChr));
                else
                {
                    rslt.Append(CharacterUtilities.MapForCharSet(range.minChr));
                    rslt.Append('-');
                    rslt.Append(CharacterUtilities.MapForCharSet(range.maxChr));
                }
            }
            rslt.Append(']');
            return rslt.ToString();
        }
#endif
    }

    /// <summary>
    /// Represents a contiguous range of characters
    /// between a given minimum and maximum values.
    /// </summary>
    internal class CharRange : IComparable<CharRange>
    {
        private static int symCard;
        internal static int SymCard { get { return symCard; } }
        internal static void Init(int num) { symCard = num; }
        internal static CharRange AllChars { get { return new CharRange(0, (symCard - 1)); } }

        internal int minChr;
        internal int maxChr;

        // internal CharRange(char min, char max) { minChr = (int)min; maxChr = (int)max; }
        internal CharRange(int min, int max) { minChr = min; maxChr = max; }

        internal CharRange(char chr) { minChr = maxChr = (int)chr; }
        internal CharRange(int chr) { minChr = maxChr = chr; }

        public override string ToString()
        {
            if (minChr == maxChr)
                return String.Format(CultureInfo.InvariantCulture, "singleton char {0}", CharacterUtilities.Map(minChr));
            else
                return String.Format(CultureInfo.InvariantCulture, "char range {0} .. {1}, {2} chars", 
                    CharacterUtilities.Map(minChr), CharacterUtilities.Map(maxChr), maxChr - minChr + 1);
        }

        public int CompareTo(CharRange rhOp)
        {
            if (minChr < rhOp.minChr)
                return -1;
            else if (minChr > rhOp.minChr)
                return +1;
            else if (maxChr > rhOp.maxChr)
                // When two ranges start at the same minChr
                // we want the longer range to come first.
                return -1;
            else if (maxChr < rhOp.maxChr)
                return +1;
            else
                return 0;
        }
    }

    /// <summary>
    /// This class represents a single partition in 
    /// a partition set. Each such element denotes
    /// a set of characters that belong to the same
    /// equivalence class with respect to the literals
    /// already processed.
    /// </summary>
    internal class PartitionElement
    {
        static int nextOrd;

        internal static void Reset()
        { nextOrd = 0; }

        internal static PartitionElement AllChars()
        {
            List<CharRange> singleton = new List<CharRange>();
            singleton.Add(CharRange.AllChars);
            return new PartitionElement(singleton, false);
        }


        internal int ord;
        internal RangeList list;

        /// <summary>
        /// List of literals that contain this partition element.
        /// </summary>
        internal List<RangeLiteral> literals = new List<RangeLiteral>();

        internal PartitionElement(List<CharRange> ranges, bool invert)
        {
            ord = nextOrd++;
            list = new RangeList(ranges, invert);
        }
    }

    /// <summary>
    /// Represents a character set literal as a
    /// list of character ranges.  A direct mapping
    /// of a LEX character set "[...]".
    /// The field equivClasses holds the list of
    /// ordinals of the partition elements that
    /// cover the characters of the literal.
    /// </summary>
    internal class RangeLiteral
    {
        internal string name;
        static RangeLiteral rAnchor;

        /// <summary>
        /// The last partition that this literal has refined
        /// </summary>
        internal Partition lastPart;

        internal RangeList list;
        internal List<int> equivClasses = new List<int>();

        internal RangeLiteral(bool invert) { list = new RangeList(invert); }
        internal RangeLiteral(int ch)
        {
            list = new RangeList(false);
            list.Add(new CharRange(ch, ch)); // AddToRange
        }
        private RangeLiteral(int lo, int hi) {
            list = new RangeList(false);
            list.Add(new CharRange(lo, lo));
            list.Add(new CharRange(hi, hi));
            list.Canonicalize();
        }

        /// <summary>
        /// If ch represents a charater with different upper and lower
        /// case codes, return a RangeLiteral representing the pair.
        /// Otherwise return a singleton RangeLiteral.
        /// </summary>
        /// <param name="ch">the code point to test</param>
        /// <returns>a pair or a singleton RangeLiteral</returns>
        internal static RangeLiteral NewCaseAgnosticPair(int ch) {
            if (ch < Char.MaxValue) {
                char c = (char)ch;
                char lo = char.ToLower(c);
                char hi = char.ToUpper(c);
                if (lo != hi)
                    return new RangeLiteral(lo, hi);
            }
            return new RangeLiteral(ch);
        }

        public override string ToString() { return name; }

        /// <summary>
        /// The RangeLiteral for all line-end characters.
        /// For ASCII case just [\n\r], for 
        /// unicode [\r\n\u0085\u2028\u2029]
        /// </summary>
        internal static RangeLiteral RightAnchors { 
            get 
            {
                if (rAnchor == null)
                {
                    rAnchor = new RangeLiteral(false);
                    rAnchor.list.Add(new CharRange('\r'));
                    rAnchor.list.Add(new CharRange('\n'));
                    if (CharRange.SymCard > 256)
                    {
                        rAnchor.list.Add(new CharRange('\x85'));
                        rAnchor.list.Add(new CharRange('\u2028', '\u2029'));
                    }
                }
                return rAnchor;
            }
        }
    }

    /// <summary>
    /// For the compression of sparse maps, adjacent sequences
    /// of character values are denoted as singletons (a single char),
    /// shortRuns (a run of identical values but shorter than CUTOFF)
    /// longRuns (a run of identical values longer than CUTOFF) and
    /// mixed values (a run containing two or more different values.
    /// </summary>
    internal class MapRun
    {
        internal enum TagType { empty, singleton, shortRun, longRun, mixedValues }

        internal TagType tag = TagType.empty;
        internal CharRange range;
        private int tableOrd = -1;
        private int hash;

        internal MapRun(int min, int max, int val)
        {
            hash = (max - min + 1) * val;
            range = new CharRange(min, max);
            if (min == max)
                tag = TagType.singleton;
            else if (max - min + 1 >= Partition.CutOff)
                tag = TagType.longRun;
            else
                tag = TagType.shortRun;
        }

        internal MapRun(int min, int max) : this(min, max, 0) {}

        internal int Length { 
            get { return ((int)range.maxChr - (int)range.minChr + 1); } 
        }

        internal int TableOrd { get { return tableOrd; } set { tableOrd = value; } }

        internal void Merge(int min, int max) { Merge(min, max, 0); }

        internal void Merge(int min, int max, int val)
        {
            if (this.range.maxChr != (min - 1)) 
                throw new GplexInternalException("Bad MapRun Merge");
            this.range.maxChr = max;
            this.tag = TagType.mixedValues;
            this.hash += (max - min + 1) * val;
        }
    }
}
