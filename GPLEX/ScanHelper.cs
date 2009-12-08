// Gardens Point Scanner Generator
// Copyright (c) K John Gough, QUT 2006-2008
// (see accompanying GPLEXcopyright.rtf.

//  Scan helper for 0.9.2 version of gplex
//  kjg 08 September 2008 2006
//

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using QUT.Gplex.Parser;

namespace QUT.Gplex.Lexer
{
    internal partial class Scanner
    {
        public ErrorHandler yyhdlr;
        private int badCount;

        static Tokens GetIdToken(string str)
        {
            switch (str[0])
            {
                case 'a':
                    if (str.Equals("abstract") || str.Equals("as"))
                        return Tokens.csKeyword;
                    break;
                case 'b':
                    if (str.Equals("base") || str.Equals("bool") ||
                        str.Equals("break") || str.Equals("byte"))
                        return Tokens.csKeyword;
                    break;
                case 'c':
                    if (str.Equals("case") || str.Equals("catch")
                     || str.Equals("char") || str.Equals("checked")
                     || str.Equals("class") || str.Equals("const")
                     || str.Equals("continue"))
                        return Tokens.csKeyword;
                    break;
                case 'd':
                    if (str.Equals("decimal") || str.Equals("default")
                     || str.Equals("delegate") || str.Equals("do")
                     || str.Equals("double"))
                        return Tokens.csKeyword;
                    break;
                case 'e':
                    if (str.Equals("else") || str.Equals("enum")
                     || str.Equals("event") || str.Equals("explicit")
                     || str.Equals("extern"))
                        return Tokens.csKeyword;
                    break;
                case 'f':
                    if (str.Equals("false") || str.Equals("finally")
                     || str.Equals("fixed") || str.Equals("float")
                     || str.Equals("for") || str.Equals("foreach"))
                        return Tokens.csKeyword;
                    break;
                case 'g':
                    if (str.Equals("goto"))
                        return Tokens.csKeyword;
                    break;
                case 'i':
                    if (str.Equals("if")
                     || str.Equals("int") || str.Equals("implicit")
                     || str.Equals("in") || str.Equals("interface")
                     || str.Equals("internal") || str.Equals("is"))
                        return Tokens.csKeyword;
                    break;
                case 'l':
                    if (str.Equals("lock") || str.Equals("long"))
                        return Tokens.csKeyword;
                    break;
                case 'n':
                    if (str.Equals("namespace") || str.Equals("new")
                     || str.Equals("null"))
                        return Tokens.csKeyword;
                    break;
                case 'o':
                    if (str.Equals("object") || str.Equals("operator")
                     || str.Equals("out") || str.Equals("override"))
                        return Tokens.csKeyword;
                    break;
                case 'p':
                    if (str.Equals("params") || str.Equals("private")
                     || str.Equals("protected") || str.Equals("public"))
                        return Tokens.csKeyword;
                    break;
                case 'r':
                    if (str.Equals("readonly") || str.Equals("ref")
                     || str.Equals("return"))
                        return Tokens.csKeyword;
                    break;
                case 's':
                    if (str.Equals("sbyte") || str.Equals("sealed")
                     || str.Equals("short") || str.Equals("sizeof")
                     || str.Equals("stackalloc") || str.Equals("static")
                     || str.Equals("string") || str.Equals("struct")
                     || str.Equals("switch"))
                        return Tokens.csKeyword;
                    break;
                case 't':
                    if (str.Equals("this") || str.Equals("throw")
                     || str.Equals("true") || str.Equals("try")
                     || str.Equals("typeof"))
                        return Tokens.csKeyword;
                    break;
                case 'u':
                    if (str.Equals("uint") || str.Equals("ulong")
                     || str.Equals("unchecked") || str.Equals("unsafe")
                     || str.Equals("ushort") || str.Equals("using"))
                        return Tokens.csKeyword;
                    break;
                case 'v':
                    if (str.Equals("virtual") || str.Equals("void"))
                        return Tokens.csKeyword;
                    break;
                case 'w':
                    if (str.Equals("while") || str.Equals("where"))
                        return Tokens.csKeyword;
                    break;
            }
            return Tokens.csIdent;
        }

        Tokens GetTagToken(string str)
        {
            switch (str)
            {
                case "%x":
                    yy_push_state(NMLST); return Tokens.exclTag;
                case "%s":
                    yy_push_state(NMLST); return Tokens.inclTag;
                case "%using":
                    yy_push_state(LCODE); return Tokens.usingTag;
                case "%scanbasetype":
                    yy_push_state(LCODE); return Tokens.scanbaseTag;
                case "%tokentype":
                    yy_push_state(LCODE); return Tokens.tokentypeTag;
                case "%scannertype":
                    yy_push_state(LCODE); return Tokens.scannertypeTag;
                case "%namespace":
                    yy_push_state(LCODE); return Tokens.namespaceTag;
                case "%option":
                    yy_push_state(VRBTM); return Tokens.optionTag;
                case "%charSetPredicate":
                case "%charClassPredicate":
                    yy_push_state(NMLST); return Tokens.charSetPredTag;
                case "%userCharPredicate":
                    yy_push_state(LCODE); return Tokens.userCharPredTag;
                case "%visibility":
                    yy_push_state(LCODE); return Tokens.visibilityTag;
                default:
                    Error(77, TokenSpan()); return Tokens.repErr;
            }
        }

        public override void yyerror(string format, params object[] args)
        { if (yyhdlr != null) yyhdlr.ListError(TokenSpan(), 1, format); }

        internal void Error(int n, LexSpan s)
        {
            // Console.WriteLine(StateStack(YY_START));
            if (yyhdlr != null) yyhdlr.ListError(s, n);
        }

        internal void ResetBadCount() { badCount = 0; }

        internal void Error79(LexSpan s)
        {
            Error(79, s); 
            badCount++;
            if (badCount >= 3)
                yy_push_state(SKIP);
        }

        internal LexSpan TokenSpan()
        { return new LexSpan(tokLin, tokCol, tokELin, tokECol, tokPos, tokEPos, buffer); }

#if STATE_DIAGNOSTICS
        public static string StateStr(int s)
        {
            switch (s)
            {
                case INITIAL: return "0";
                case RULES: return "RULES";
                case UCODE: return "UCODE";
                case LCODE: return "LCODE";
                case BCODE: return "BCODE";
                case INDNT: return "INDNT";
                case CMMNT: return "CMMNT";
                case SMACT: return "SMACT";
                case XPEOL: return "XPEOL";
                case REGEX: return "REGEX";
                case NMLST: return "NMLST";
                case SPACE: return "SPACE";
                case VRBTM: return "VRBTM";
                case PRGRP: return "PRGRP";
                case SKIP: return "SKIP";
                default: return "state " + s.ToString();
            }
        }

        public string StateStack(int s)
        {
            string rslt = StateStr(s);
            int[] arry = scStack.ToArray();
            for (int i = 0; i < scStack.Count; i++)
                rslt += (":" + StateStr(arry[i]));
            return rslt;
        }
#endif

        int depth;
        LexSpan comStart;
    }
}
