// Gardens Point Scanner Generator
// Copyright (c) K John Gough, QUT 2006-2014
// (see accompanying GPLEXcopyright.rtf)


using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using System.Diagnostics.CodeAnalysis;

namespace QUT.Gplex
{

    public interface ICharTestFactory
    {
        CharTest GetDelegate(string name);
    }

    /// <summary>
    /// Delegate type for predicates over code points.
    /// </summary>
    /// <param name="ord"></param>
    /// <returns></returns>
    public delegate bool CharTest(int ordinal);

    /// <summary>
    /// Delegate type for applying a predicate to a char value.
    /// Note that this only applies to code points in the 
    /// basic multilingual plane, i.e. chr &lt; Char.MaxValue
    /// </summary>
    /// <param name="chr"></param>
    /// <returns></returns>
    internal delegate bool CharPredicate(char chr);

    /// <summary>
    /// Delegate type for applying a predicate to code point.
    /// The code point might be represented by a surrogate 
    /// pair at the prescribed position in the string.
    /// </summary>
    /// <param name="str"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    internal delegate bool CodePointPredicate(string str, int index);

    /// <summary>
    /// This class maps from a character ordinal to a character class.
    /// A well-formed CharClassMap must map to a class for every 
    /// index in the complete character ordinal range.
    /// </summary>
    internal class CharClassMap
    {
        // ==========================================================
        /// <summary>
        /// Nodes to represent contiguous ranges with same
        /// mapping in the Class Map. "value" is the map class 
        /// ordinal for all indices in the range min..max.
        /// </summary>
        class TreeNode
        {
            int min;
            int max;
            int value;
            TreeNode lKid;
            TreeNode rKid;

            internal TreeNode(int mn, int mx, int vl)
            {
                min = mn; max = mx; value = vl;
            }

            /// <summary>
            /// Fetch the bounds of the range that contains "index"
            /// </summary>
            /// <param name="index">Index value to lookup</param>
            /// <param name="rMin">Low bound of range</param>
            /// <param name="rMax">High bound of range</param>
            internal void GetRange(int index, out int rMin, out int rMax)
            {
                if (index < min) lKid.GetRange(index, out rMin, out rMax);
                else if (index > max) rKid.GetRange(index, out rMin, out rMax);
                else 
                {
                    rMin = min; 
                    rMax = max; 
                }
            }

            /// <summary>
            /// Lookup the class ordinal for the range that contains index.
            /// </summary>
            /// <param name="index">The index to check</param>
            /// <returns>The class ordinal of the range</returns>
            internal int Lookup(int index)
            {
                if (index < min) return lKid.Lookup(index);
                else if (index > max) return rKid.Lookup(index);
                else return value;
            }

            /// <summary>
            /// Insert a new Node in the tree.
            /// </summary>
            /// <param name="node">Node to insert</param>
            internal void InsertNewNode(TreeNode node)
            {
                int key = node.min;
                if (key < this.min)
                {
                    if (this.lKid == null)
                        this.lKid = node;
                    else
                        lKid.InsertNewNode(node);
                }
                else if (key > this.max)
                {
                    if (this.rKid == null)
                        this.rKid = node;
                    else
                        rKid.InsertNewNode(node);
                }
                else throw new Parser.GplexInternalException("Invalid range overlap");
            }
        }
        // ==========================================================

        int count;
        TreeNode root;

        internal int this[int theChar]
        {
            get
            {
                if (root == null) throw new Parser.GplexInternalException("Map not initialized");
                return root.Lookup(theChar);
            }
        }

        internal void GetEnclosingRange(int theChar, out int min, out int max)
        {
            root.GetRange(theChar, out min, out max);
        }

        /// <summary>
        /// Create a well-formed (that is, *complete*) 
        /// map from a GPLEX.Parser.Partition.
        /// </summary>
        /// <param name="part">The Partition to use</param>
        internal CharClassMap(Parser.Partition part)
        {

            foreach (Parser.PartitionElement pElem in part.elements)
            {
                foreach (Parser.CharRange range in pElem.list.Ranges)
                {
                    count++; 
                    TreeNode node = new TreeNode(range.minChr, range.maxChr, pElem.ord);
                    if (root == null) root = node; else root.InsertNewNode(node);
                }
            }
        }

        //internal int Depth { get { return root.Depth; } }
        //internal int Count { get { return this.count; } }
    }

    /// <summary>
    /// A Membership Set ADT implemented as a binary tree.
    /// Insert, Delete and Lookup implemented.
    /// </summary>
    internal class TreeSet
    {
        // ===================================================
        /// <summary>
        /// This class defines nodes for use in a set
        /// ADT based on binary trees. In this application
        /// we do not need deletion.
        /// </summary>
        class Node
        {
            Node lKid;
            Node rKid;
            int value;

            internal Node(int value) { this.value = value; }

            //private bool IsLeaf { get { return lKid == null && rKid == null; } }

            /// <summary>
            /// Check if key is a member of the set
            /// </summary>
            /// <param name="key">The key to check</param>
            /// <returns></returns>
            internal bool Lookup(int key)
            {
                if (key == value) return true;
                if (key < value) 
                    return lKid != null && lKid.Lookup(key);
                else // (key > value)
                    return rKid != null && rKid.Lookup(key);
            }

            /// <summary>
            /// Insert key, if not already present.
            /// </summary>
            /// <param name="key">Value to insert</param>
            internal void Insert(int key)
            {
                if (key == value) return; // key is already present
                if (key < value)
                {
                    if (lKid == null)
                        lKid = new Node(key);
                    else
                        lKid.Insert(key);
                }
                else
                {
                    if (rKid == null)
                        rKid = new Node(key);
                    else
                        rKid.Insert(key);
                }
            }

            /// <summary>
            /// Delete key from tree rooted at tree if the key
            /// is present in the tree.  This is a static method 
            /// with a ref param to handle the special case of 
            /// deletion of a leaf node.
            /// </summary>
            /// <param name="tree"></param>
            /// <param name="key"></param>
            internal static void Delete(ref Node tree, int key)
            {
                if (tree == null)
                    return;
                else if (!tree.RemovedOk(key))
                    tree = null;
            }

            private bool RemovedOk(int key)
            {
                if (value == key)
                {
                    Node replacement = null;
                    if (lKid != null)
                    {
                        replacement = lKid.Largest();
                        value = replacement.value;
                        Delete(ref lKid, value);
                        return true;
                    }
                    else if (rKid != null)
                    {
                        replacement = rKid.Smallest();
                        value = replacement.value;
                        Delete(ref rKid, value);
                        return true;
                    }
                    else
                        return false;
                }
                else if (key < value && lKid != null)
                {
                    if (!lKid.RemovedOk(key)) lKid = null;
                }
                else if (key > value && rKid != null)
                {
                    if (!rKid.RemovedOk(key)) rKid = null;
                }
                return true; // No such member!
            }

            private Node Largest()
            {
                if (rKid == null) return this; else return rKid.Largest();
            }

            private Node Smallest()
            {
                if (lKid == null) return this; else return lKid.Smallest();
            }
        }
        // ===================================================

        private Node root;

        internal bool this[int thisChar]
        {
            get { return root != null && root.Lookup(thisChar); }
            set 
            {
                if (value == false)
                    Node.Delete(ref root, thisChar);
                else if (root == null)
                    root = new Node(thisChar);
                else
                    root.Insert(thisChar);
            }
        }
    }

    internal static class CharCategory
    {
        static BitArray idStart = new BitArray(32, false);
        static BitArray idPart = new BitArray(32, false);

        /// <summary>
        /// This method builds bit-sets to represent the UnicodeCategory
        /// values that belong to the IdStart and IdContinue predicate.
        /// This uses the values as defined for C# V3, with the exception
        /// that format characters (General Category Cf) are not included
        /// in the IdPart set.  This is so that those identifiers that 
        /// include a Cf character can have a different accept state in
        /// the automaton, and only these identifer need be canonicalized.
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline")]
        // Reason for message suppression: BitArray initializer for
        // enumeration types is not understandable by human readers.
        static CharCategory()
        {
            idStart[(int)UnicodeCategory.UppercaseLetter] = true;
            idStart[(int)UnicodeCategory.LowercaseLetter] = true;
            idStart[(int)UnicodeCategory.TitlecaseLetter] = true;
            idStart[(int)UnicodeCategory.ModifierLetter] = true;
            idStart[(int)UnicodeCategory.OtherLetter] = true;
            idStart[(int)UnicodeCategory.LetterNumber] = true;

            idPart[(int)UnicodeCategory.UppercaseLetter] = true;
            idPart[(int)UnicodeCategory.LowercaseLetter] = true;
            idPart[(int)UnicodeCategory.TitlecaseLetter] = true;
            idPart[(int)UnicodeCategory.ModifierLetter] = true;
            idPart[(int)UnicodeCategory.OtherLetter] = true;
            idPart[(int)UnicodeCategory.LetterNumber] = true;

            idPart[(int)UnicodeCategory.DecimalDigitNumber] = true;

            idPart[(int)UnicodeCategory.ConnectorPunctuation] = true;

            idPart[(int)UnicodeCategory.NonSpacingMark] = true;
            idPart[(int)UnicodeCategory.SpacingCombiningMark] = true;

            // Format characters are not included, so that we
            // may use a different semantic action with identifiers
            // that require canonicalization.
            //
            // idPart[(int)UnicodeCategory.Format] = true;
        }

        //
        //  These predicates are only used at compile time, when building
        //  the multilevel structure that performs the tests at runtime.
        //
        internal static bool IsIdStart(char chr)
        {
            if (chr == '_')
                return true;
            UnicodeCategory theCat = Char.GetUnicodeCategory(chr);
            return idStart[(int)theCat];
        }

        internal static bool IsIdStart(string str, int index)
        {
            if (str[index] == '_')
                return true;
            UnicodeCategory theCat = Char.GetUnicodeCategory(str, index);
            return idStart[(int)theCat];
        }

        internal static bool IsIdPart(char chr)
        {
            UnicodeCategory theCat = Char.GetUnicodeCategory(chr);
            return idPart[(int)theCat];
        }

        internal static bool IsIdPart(string str, int index)
        {
            UnicodeCategory theCat = Char.GetUnicodeCategory(str, index);
            return idPart[(int)theCat];
        }

        internal static bool IsFormat(char chr)
        {
            UnicodeCategory theCat = Char.GetUnicodeCategory(chr);
            return theCat == UnicodeCategory.Format;
        }

        internal static bool IsFormat(string str, int index)
        {
            UnicodeCategory theCat = Char.GetUnicodeCategory(str, index);
            return theCat == UnicodeCategory.Format;
        }
    }
}
