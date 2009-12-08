// Gardens Point Scanner Generator
// Copyright (c) K John Gough, QUT 2006-2008
// (see accompanying GPLEXcopyright.rtf)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using QUT.Gplex.Parser;

namespace QUT.Gplex.Automaton
{
    class NFSA
    {
        TaskState task;

        public NfsaInstance[] nfas;
        public Dictionary<uint, NState> next = new Dictionary<uint, NState>();

        public NFSA(TaskState t) { task = t; }

        /// <summary>
        /// Build the NFSA from the abstract syntax tree.
        /// There is an NfsaInstance for each start state.
        /// Each rule starts with a new nfsa state, which
        /// is the target of a new epsilon transition from
        /// the real start state, nInst.Entry.
        /// </summary>
        /// <param name="ast"></param>
        public void Build(AAST ast)
        {
            int index = 0;
            DateTime time0 = DateTime.Now;
            nfas = new NfsaInstance[ast.StartStateCount];
            foreach (KeyValuePair<string, StartState> p in ast.startStates)
            {
                StartState s = p.Value;
                string name = p.Key;
                if (!s.IsAll)
                {
                    NfsaInstance nInst = new NfsaInstance(s, this);
                    nfas[index++] = nInst;
                    nInst.key = name;

                    // for each pattern do ...
                    for (int i = 0; i < s.rules.Count; i++)
                    {
                        RuleDesc rule = s.rules[i];
                        RegExTree tree = rule.Tree;

                        if (rule.isPredDummyRule)
                        {
                            NState entry = nInst.Entry;
                            nInst.MakePath(tree, entry, entry);
                        }
                        else
                        {
                            NState start = nInst.MkState();
                            NState endSt = nInst.MkState();

                            if (tree.op == RegOp.leftAnchor)     // this is a left anchored pattern
                            {
                                nInst.AnchorState.AddEpsTrns(start);
                                tree = ((Unary)tree).kid;
                            }
                            else                                // this is not a left anchored pattern
                                nInst.Entry.AddEpsTrns(start);
                            //
                            // Now check for right anchors, and add states as necessary.
                            //
                            if (tree.op == RegOp.eof)
                            {
                                //
                                // <<EOF>> rules are always emitted outside
                                // of the usual subset construction framework.
                                // We ensure that we do not get spurious warnings.
                                //
                                rule.useCount = 1;
                                nInst.eofAction = rule.aSpan;
                                nInst.MakePath(tree, start, endSt);
                                nInst.MarkAccept(endSt, rule);
                            }
                            else if (tree.op == RegOp.rightAnchor)
                            {
                                tree = ((Unary)tree).kid;
                                nInst.MakePath(tree, start, endSt);
                                AddAnchorContext(nInst, endSt, rule);
                            }
                            else
                            {
                                nInst.MakePath(tree, start, endSt);
                                nInst.MarkAccept(endSt, rule);
                            }
                        }
                    }
                }
            }
            if (task.Verbose)
            {
                Console.Write("GPLEX: NFSA built");
                Console.Write((task.Errors ? ", errors detected" : " without error"));
                Console.Write((task.Warnings ? "; warnings issued. " : ". "));
                Console.WriteLine(TaskState.ElapsedTime(time0));
            }
            if (task.Summary)
                WriteSummary(time0);
        }

        //
        // For version 1.0.1 recognize any line-end character if /unicode
        //
        static void AddAnchorContext(NfsaInstance nInst, NState endS, RuleDesc rule)
        {
            NState nEnd = nInst.MkState();
            Leaf temp = new Leaf(RegOp.charClass);
            temp.rangeLit = RangeLiteral.RightAnchors;
            nInst.MakePath(temp, endS, nEnd);
            nInst.MarkAccept(nEnd, rule);
            nEnd.rhCntx = 1;
        }

        void WriteSummary(DateTime time)
        {
            task.ListStream.WriteLine("/*");
            task.ListDivider();
            task.ListStream.WriteLine("NFSA Summary for input file <" + task.FileName + ">");
            task.ListDivider();
            task.ListStream.WriteLine("Number of Start Conditions = " + (nfas.Length - 1));
            for (int i = 0; i < nfas.Length; i++)
            {
                NfsaInstance inst = nfas[i];
                if (inst != null)
                {
                    task.ListStream.WriteLine("Start condition " + inst.key + ":");
                    task.ListStream.Write("  number of patterns = " + inst.myStartCondition.rules.Count);
                    task.ListStream.Write(", number of nfsa states = " + inst.nStates.Count);
                    task.ListStream.WriteLine(", accept states = " + inst.acceptStates.Count);
                }
            }

            task.ListDivider();
            task.ListStream.Write("GPLEX: NFSA built. ");
            task.ListStream.WriteLine(TaskState.ElapsedTime(time));
            task.ListStream.Flush();
        }

        /// <summary>
        /// This class represents the NFSA for all the regular expressions that 
        /// begin on a given start condition. Each NfsaInstance may have two "start"
        /// states, corresponding to the anchored and unanchored pattern starts.
        /// </summary>
        public class NfsaInstance
        {
            const int defN = 32;
            internal string key;
            internal LexSpan eofAction;
            internal StartState myStartCondition;           // from the LEX start state
            internal List<int> acceptStates = new List<int>();
            internal List<NState> nStates = new List<NState>();
            internal NFSA parent;

            private bool pack;
            private int maxE = defN;                       // number of elements in epsilon BitArray
            private int maxS;

            NState anchorState;
            NState entryState;

            public NfsaInstance(StartState ss, NFSA parent)
            {
                myStartCondition = ss;
                this.parent = parent;
                this.pack = parent.task.ChrClasses;
                if (pack)
                    maxS = parent.task.partition.Length;          // Number of equivalence classes
                else
                    maxS = parent.task.TargetSymCardinality;      // Size of alphabet
                entryState = MkState();
            }

            /// <summary>
            /// The target alphabet cardinality
            /// </summary>
            internal int MaxSym { get { return maxS; } }

            internal bool Pack { get { return pack; } }

            /// <summary>
            /// The current size of the dynamically sized epsilon list
            /// </summary>
            internal int MaxEps { get { return maxE; } }

            internal bool LeftAnchored { get { return anchorState != null; } }

            internal NState Entry { get { return entryState; } }

            internal NState MkState()
            {
                NState s = new NState(this);
                s.ord = nStates.Count;
                if (s.ord >= maxE) maxE *= 2;
                nStates.Add(s);
                return s;
            }

            /// <summary>
            /// NfsaInst objects only have an anchorState if there is one or more
            /// left-anchored patterns for the corresponding start condition.
            /// AnchorState is allocated on demand when the first such pattern
            /// is discoverd during MakePath.
            /// </summary>
            internal NState AnchorState
            {
                get
                {
                    if (anchorState == null)
                    {
                        anchorState = MkState();
                        anchorState.AddEpsTrns(entryState);
                    }
                    return anchorState;
                }
            }


            internal void MarkAccept(NState acpt, RuleDesc rule)
            {
                acpt.accept = rule;
                acceptStates.Add(acpt.ord);
            }

            /// <summary>
            /// Create a transition path in the NFSA from the given 
            /// start state to the given end state, corresponding to the
            /// RegEx tree value.  The method may (almost always does)
            /// create new NFSA states and recurses to make paths for
            /// the subtrees of the given tree.
            /// </summary>
            /// <param name="tree">The tree to encode</param>
            /// <param name="start">The start state for the pattern</param>
            /// <param name="end">The end state for the pattern</param>
            internal void MakePath(RegExTree tree, NState startState, NState endState)
            {
                NState tmp1 = null;
                NState tmp2 = null;
                int rLen, lLen;

                switch (tree.op)
                {
                    case RegOp.eof:
                        break;
                    // Binary nodes ===================================
                    case RegOp.context:
                    case RegOp.concat:
                    case RegOp.alt:
                        // Binary nodes ===================================
                        Binary binNode = tree as Binary;
                        switch (tree.op)
                        {
                            case RegOp.context:
                                rLen = binNode.rKid.contextLength();
                                lLen = binNode.lKid.contextLength();
                                if (rLen <= 0 && lLen <= 0)
                                    throw new StringInterpretException("variable right context '/' not implemented");
                                else
                                {
                                    endState.rhCntx = rLen;
                                    endState.lhCntx = lLen;
                                    tmp1 = MkState();
                                    MakePath(binNode.lKid, startState, tmp1);
                                    MakePath(binNode.rKid, tmp1, endState);
                                }
                                break;
                            case RegOp.concat:
                                tmp1 = MkState();
                                MakePath(binNode.lKid, startState, tmp1);
                                MakePath(binNode.rKid, tmp1, endState);
                                break;
                            case RegOp.alt:
                                tmp1 = MkState();
                                MakePath(binNode.lKid, startState, tmp1);
                                tmp1.AddEpsTrns(endState);
                                tmp1 = MkState();
                                MakePath(binNode.rKid, startState, tmp1);
                                tmp1.AddEpsTrns(endState);
                                break;
                        }
                        break;
                    // Unary nodes ===================================
                    case RegOp.closure:
                    case RegOp.finiteRep:
                        // Unary nodes ===================================
                        Unary unaryNode = tree as Unary;
                        switch (tree.op)
                        {
                            case RegOp.closure:
                                tmp2 = MkState();
                                if (unaryNode.minRep == 0)
                                {
                                    tmp1 = MkState();
                                    startState.AddEpsTrns(tmp1);
                                }
                                else
                                {
                                    NState dummy = startState;
                                    for (int i = 0; i < unaryNode.minRep; i++)
                                    {
                                        tmp1 = MkState();
                                        MakePath(unaryNode.kid, dummy, tmp1);
                                        dummy = tmp1;
                                    }
                                }
                                MakePath(unaryNode.kid, tmp1, tmp2);
                                tmp2.AddEpsTrns(tmp1);
                                tmp1.AddEpsTrns(endState);
                                break;
                            case RegOp.finiteRep:
                                {
                                    NState dummy = tmp1 = startState;
                                    for (int i = 0; i < unaryNode.minRep; i++)
                                    {
                                        tmp1 = MkState();
                                        MakePath(unaryNode.kid, dummy, tmp1);
                                        dummy = tmp1;
                                    }
                                    tmp1.AddEpsTrns(endState);
                                    for (int i = unaryNode.minRep; i < unaryNode.maxRep; i++)
                                    {
                                        tmp1 = MkState();
                                        MakePath(unaryNode.kid, dummy, tmp1);
                                        dummy = tmp1;
                                        dummy.AddEpsTrns(endState);
                                    }
                                }
                                break;
                        }
                        break;
                    // Leaf nodes ===================================
                    case RegOp.litStr:
                    case RegOp.primitive:
                    case RegOp.charClass:
                        // Leaf nodes ===================================
                        Leaf leafNode = tree as Leaf;
                        switch (tree.op)
                        {
                            case RegOp.litStr:
                                {
                                    // Make a linear sequence of states with successive
                                    // transitions on successive string characters.
                                    //
                                    string text = leafNode.str;
                                    NState dummy = startState;
                                    // Need to deal with special case of empty string
                                    if (text.Length == 0)
                                        dummy.AddEpsTrns(endState);
                                    else
                                    {
                                        //  This code is complicated by the fact that unicode
                                        //  escape substitution may have inserted surrogate
                                        //  pairs of characters in the string.  We need
                                        //  one transition for every unicode character,
                                        //  not one for every char value in this string.
                                        //
                                        int index = 0;
                                        int code = CharacterUtilities.CodePoint(text, ref index); // First character
                                        int next = CharacterUtilities.CodePoint(text, ref index); // Next, possibly -1
                                        while (next >= 0)
                                        {
                                            tmp1 = MkState();
                                            dummy.AddChrTrns(code, tmp1);
                                            dummy = tmp1;
                                            code = next;
                                            next = CharacterUtilities.CodePoint(text, ref index);
                                        }
                                        // Postcondition ==> "code" is the last char.
                                        dummy.AddChrTrns(code, endState);
                                    }
                                }
                                break;
                            case RegOp.primitive:
                                startState.AddChrTrns(leafNode.chVal, endState);
                                break;

                            case RegOp.charClass:
                                startState.AddClsTrans(leafNode, endState);
                                break;
                        }
                        break;
                    default: throw new GplexInternalException("unknown tree op");
                }
            }

        }


        /// <summary>
        /// This class represents a state in the NFSA
        /// each state has an array of transitions on
        /// a particular character value, and a list 
        /// *and* bitarray of epsilon transitions.
        /// We want to go "for every epsilon do"
        /// and also do bitwise boolean ops.
        /// </summary>
        public class NState
        {
            private static uint nextSN;

            NfsaInstance myNfaInst;
            NFSA myNfsa;
            internal int ord;
            private uint serialNumber;
            internal BitArray epsilons;                 // epsilon transitions.
            internal List<NState> epsList = new List<NState>();
            internal RuleDesc accept;                   // rule matched OR null
            internal int rhCntx;                        // length of fixed right context
            internal int lhCntx;                        // length of fixed context lhs

            public NState(NfsaInstance elem)
            {
                myNfaInst = elem;
                myNfsa = elem.parent;
                serialNumber = (ushort)nextSN++;
                epsilons = new BitArray(myNfaInst.MaxEps);    // Caller adds to nStates list.
            }

            internal NState GetNext(int sym)
            {
                NState rslt = null;
                uint key = (this.serialNumber << 16) + (ushort)sym;
                myNfsa.next.TryGetValue(key, out rslt);
                return rslt;
            }

            internal void SetNext(int sym, NState dstState)
            {
                uint key = (this.serialNumber << 16) + (ushort)sym;
                myNfsa.next.Add(key, dstState);
            }

            /// <summary>
            /// Add a transition from NState "this"
            /// to NState "nxt", for the character "chr".
            /// If the characters are packed, transform 
            /// from character ordinal to equivalence class 
            /// ordinal.
            /// </summary>
            /// <param name="chr">The character value</param>
            /// <param name="nxt">The destination state</param>
            public void AddChrTrns(int chr, NState nxt)
            {
                if (myNfaInst.Pack)
                    chr = myNfaInst.parent.task.partition[chr];
                AddRawTransition(chr, nxt);
            }

            /// <summary>
            /// Add a transition to the NState.
            /// Assert: if the symbol ordinals are packed
            /// the mapping has already been performed
            /// </summary>
            /// <param name="ord">The symbol index</param>
            /// <param name="nxt">The destination state</param>
            private void AddRawTransition(int ord, NState nxt)
            {
                if (GetNext(ord) == null)
                    SetNext(ord, nxt);
                else        // state must have overlapping alternatives
                {
                    NState temp = myNfaInst.MkState();
                    this.AddEpsTrns(temp);
                    temp.AddRawTransition(ord, nxt);
                }
            }

            /// <summary>
            /// Add a transition from "this" to "next"
            /// for every true bit in the BitArray cls
            /// </summary>
            /// <param name="cls">The transition bit array</param>
            /// <param name="nxt">The destination state</param>
            private void AddClsTrans(BitArray cls, NState nxt)
            {
                for (int i = 0; i < cls.Count; i++)
                    if (cls[i]) AddRawTransition(i, nxt);
            }

            /// <summary>
            /// Add a transition from NState "this"
            /// to NState "nxt", for each character
            /// value in the leaf range list.
            /// If the characters are packed, transform 
            /// from character ordinal to equivalence class 
            /// ordinal.
            /// </summary>
            /// <param name="leaf">The regex leaf node</param>
            /// <param name="nxt">The destination state</param>
            public void AddClsTrans(Leaf leaf, NState nxt)
            {
                BitArray cls = new BitArray(myNfaInst.MaxSym);
                if (myNfaInst.Pack)
                {
                    foreach (int ord in leaf.rangeLit.equivClasses)
                        cls[ord] = true;
                }
                else
                {
                    foreach (CharRange rng in leaf.rangeLit.list.Ranges)
                        for (int i = rng.minChr; i <= rng.maxChr; i++)
                            cls[i] = true;
                    if (leaf.rangeLit.list.IsInverted)
                        cls = cls.Not();
                }
                AddClsTrans(cls, nxt);
            }

            /// <summary>
            /// Add an epsilon transition from "this" to "nxt"
            /// </summary>
            /// <param name="nxt">Destination state</param>
            public void AddEpsTrns(NState nxt)
            {
                int count = epsilons.Count;
                if (count < myNfaInst.MaxEps) epsilons.Length = myNfaInst.MaxEps;
                if (!epsilons[nxt.ord])
                {
                    epsList.Add(nxt);
                    epsilons[nxt.ord] = true;
                }
            }

        }
    }
}
