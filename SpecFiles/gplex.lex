

// =============================================================

%using System.Collections;
%using QUT.Gplex.Parser;
%namespace QUT.Gplex.Lexer

%visibility internal

%option stack, classes, minimize, parser, verbose, persistbuffer, noembedbuffers, out:Scanner.cs

%{
        // User code is all now in ScanHelper.cs
%}

// =============================================================
// =============================================================

Eol             (\r\n?|\n)
NotWh           [^ \t\r\n]
Space           [ \t]
Ident           [a-zA-Z_][a-zA-Z0-9_]*
Number          [0-9]+
OctDig          [0-7]
HexDig          [0-9a-fA-F]

CmntStrt     \/\*
CmntEnd      \*\/
ABStar       [^\*\n\r]*

DotChr       [^\r\n]
EscChr       \\{DotChr}
OctEsc       \\{OctDig}{3}
HexEsc       \\x{HexDig}{2}
UniEsc       \\u{HexDig}{4}
UNIESC       \\U{HexDig}{8}
VerbChr      [^\"]

ClsChs       [^\\\]\r\n]
ChrCls       \[({ClsChs}|{EscChr}|{OctEsc}|{HexEsc}|{UniEsc}|{UNIESC})+\]

StrChs       [^\\\"\a\b\f\n\r\t\v\0]
ChrChs       [^\\'\a\b\f\n\r\t\v\0]

LitChr       \'({ChrChs}|{EscChr}|{OctEsc}|{HexEsc}|{UniEsc}|{UNIESC})\'
LitStr       \"({StrChs}|{EscChr}|{OctEsc}|{HexEsc}|{UniEsc}|{UNIESC})*\"
BadStr       \"({StrChs}|{EscChr}|{OctEsc}|{HexEsc}|{UniEsc}|{UNIESC})*{Eol}
VerbStr      @\"({VerbChr}|\"\")*\"

ClsRef       \{{Ident}\}
RepMrk       \{{Number}(,{Number}?)?\}

CShOps       [+\-*/%&|<>=@?:!#]
Dgrphs       (\+\+|--|==|!=|\+=|-=|\*=|\/=|%=|>=|<=|<<|>>)

RgxChs       [^ \t\r\n\\[\"\{]
Regex        ({RgxChs}|{ClsRef}|{RepMrk}|{EscChr}|{LitStr}|{ChrCls})

OneLineCmnt  \/\/{DotChr}*

%x SKIP      // Error recovery
%x RULES     // Rules section
%x UCODE     // Possibly unindented code contained in "%{, %}" pair
%x BCODE     // Code block possibly extending over multiple lines
%x LCODE     // Single line of code in semantic action
%x INDNT     // At start of indented code section
%x CMMNT     // Inside a comment
%x SMACT     // Looking for Semantic Action start
%x XPEOL     // Expecting end of line (should be blank here)
%x REGEX     // In regular expression scanning mode
%x NMLST     // Scanning a Name List
%x SPACE     // Scanning White Space
%x VRBTM     // Scanning verbatim character sequence
%x PRGRP     // Production group with shared start list

// =============================================================
%%  // Start of rules
// =============================================================

<*>{OneLineCmnt} { return (int)Tokens.csCommentL; }

<SKIP>{DotChr}+        { yy_pop_state(); }

<SPACE>{
  {Space}+             { yy_pop_state(); }
  {NotWh}+             { Error(78, TokenSpan()); return (int)Tokens.repErr; }
  {Eol}                { yy_pop_state(); Error(78, TokenSpan()); return (int)Tokens.EOL; }
}

<VRBTM>{Space}+        { /* skip */ }
<VRBTM>{DotChr}+       { yy_pop_state(); return (int)Tokens.verbatim; }

/* All the states terminated by EOL */
<XPEOL,VRBTM,LCODE,NMLST>{Eol} { 
                         yy_pop_state(); ResetBadCount(); return (int)Tokens.EOL; 
                       }
                       
<XPEOL>{NotWh}{DotChr}*   { Error(80, TokenSpan()); return (int)Tokens.repErr; }

/* In the INITIAL start condition there is nothing to pop */
^{Ident}               { 
                         yy_push_state(REGEX); 
                         yy_push_state(SPACE); 
                         return (int)Tokens.name; 
                       }
/* 
 *  In the indented code start condition 
 *  This rule must be before the error-recovery rule
 *  that appears next.  
 */
<INDNT>^{NotWh}{DotChr}*  { yy_pop_state(); yyless(0); return (int)Tokens.lxEndIndent; }
                       
// This is an error-recovery attempt. ^%% at any stage re-starts with
// a clean sheet in the start-condition stack.
<*>^{Space}*%%         {
                         if (yytext.Length > 2)
                             Error(90, TokenSpan());
                         while (scStack.Count > 0) 
                             yy_pop_state();
                         if (YY_START == 0)
                             BEGIN(RULES);
                         else
                             BEGIN(UCODE); 
                         yy_push_state(XPEOL); 
                         return (int)Tokens.PCPC; 
                       }
                       
^%{Ident}              {
                         return (int)GetTagToken(yytext); 
                         // GetTagToken can push NMLST, LCODE or VRBTM 
                       }
^{CmntStrt}{ABStar}\** { 
                         yy_push_state(CMMNT); 
                         comStart = TokenSpan(); 
                         return (int)Tokens.defCommentS; 
                       }
^{CmntStrt}{ABStar}\**{CmntEnd} { return (int)Tokens.defCommentE; }

/* Code inclusion in either definitions or rules sections */
<0,RULES>{
  ^{Space}+/{NotWh}    { yy_push_state(INDNT); return (int)Tokens.lxIndent; }
  ^%\{                 { yy_push_state(UCODE); yy_push_state(XPEOL); return (int)Tokens.lPcBrace; }
  ^%\}                 { return (int)Tokens.rPcBrace; /* error! */ }
}
<UCODE>^%\}            { yy_pop_state(); yy_push_state(XPEOL); return (int)Tokens.rPcBrace; }

/* In the "NameList" start condition */
<NMLST>{
  {Space}+           { /* skip */ }
  {Number}           { return (int)Tokens.csNumber; }
  \*                 { return (int)Tokens.csStar; }
  ,                  { return (int)Tokens.comma; }
  {Ident}            { return (int)Tokens.name; }
  >                  { 
                       yy_pop_state(); 
                       return (int)Tokens.rCond; 
                     }
}

/* In the regular expression start condition */                                       
<REGEX>{Regex}+      { 
                       yy_pop_state();
                       switch (YY_START) {
                           case INITIAL:
                               yy_push_state(XPEOL); break;
                           case RULES:
                           case PRGRP:
                               yy_push_state(SMACT);
                               yy_push_state(SPACE); break;
                       } 
                       return (int)Tokens.pattern; 
                     }
<REGEX>\{            {
                       yy_pop_state();
                       if (YY_START == RULES || YY_START == PRGRP) {
                           yy_push_state(PRGRP);
                           yy_push_state(XPEOL);
                           return (int)Tokens.lxLBrace;
                       } else {
                           Error79(TokenSpan());
                           return (int)Tokens.repErr;
                       }
                     }
<REGEX>{Regex}*{BadStr} {
                       yy_pop_state();
                       Error(102, TokenSpan());
                       return (int)Tokens.repErr;
                     }

<INDNT,UCODE,BCODE,LCODE>{
    <RULES,PRGRP>{
    // In either indented code or user code start conditions
    // OR in a singleton rule or group rules context ...
        {CmntStrt}{ABStar}\**   {
                       yy_push_state(CMMNT);
                       comStart = TokenSpan(); 
                       return (int)Tokens.csCommentS; 
                     }
        {CmntStrt}{ABStar}\**{CmntEnd} {
                       return (int)Tokens.csCommentE; 
                     }
    }

    // more CSharp tokens, in code sections only ... 
    {Ident}          { return (int)GetIdToken(yytext); }
    {Number}         { return (int)Tokens.csNumber; }
    {CShOps}         |
    {Dgrphs}         { return (int)Tokens.csOp; }
    {LitStr}         { return (int)Tokens.csLitstr; }
    {VerbStr}         { return (int)Tokens.csVerbstr; }
    {BadStr}         {
                       Error(102, TokenSpan()); 
                       return (int)Tokens.repErr; 
                     }
    {LitChr}         { return (int)Tokens.csLitchr; }
    ,                { return (int)Tokens.comma; }
    ;                { return (int)Tokens.semi; }
    \.               { return (int)Tokens.csDot; }
    \[               { return (int)Tokens.csLBrac; }
    \]               { return (int)Tokens.csRBrac; }
    \(               { return (int)Tokens.csLPar; }
    \)               { return (int)Tokens.csRPar; }
}

<INDNT,UCODE,LCODE>\{     { return (int)Tokens.csLBrace; }
<INDNT,UCODE,LCODE>\}     { return (int)Tokens.csRBrace; }
<BCODE>\{                 { depth++; return (int)Tokens.csLBrace; }
<BCODE>\}                 {
                            if (depth > 0) { depth--; return (int)Tokens.csRBrace; }
                            else           { yy_pop_state(); return (int)Tokens.lxRBrace; }
                          }

/* Inside a CSharp comment or a LEX comment */
<CMMNT>{
  {ABStar}\**             { return (int)Tokens.csCommentS; }
  {ABStar}\**{CmntEnd}    { yy_pop_state(); return (int)Tokens.csCommentE; }
  <<EOF>>                 { Error(60, comStart); }
}

/* Inside the rules section */
<RULES>{
  ^<                 { 
                       yy_push_state(REGEX); 
                       yy_push_state(NMLST); 
                       return (int)Tokens.lCond; 
                     }
  ^{NotWh}           { 
                       yy_push_state(REGEX); 
                       yyless(0); 
                     }
  ^"<<EOF>>"         { 
                       yy_push_state(SMACT); 
                       yy_push_state(SPACE); 
                       return (int)Tokens.pattern; 
                     }
}

<PRGRP>{
    ^{Space}+          /* skip */
    \}               {
                       yy_pop_state();
                       yy_push_state(XPEOL);
                       return (int)Tokens.lxRBrace;
                     }
    \<               { 
                       yy_push_state(REGEX); 
                       yy_push_state(NMLST); 
                       return (int)Tokens.lCond; 
                     }                   
    {NotWh}          { yy_push_state(REGEX); yyless(0); }
    "<<EOF>>"        { 
                       yy_push_state(SMACT); 
                       yy_push_state(SPACE); 
                       return (int)Tokens.pattern;
                     } 
}

<SMACT>{
  \|                 { yy_pop_state(); return (int)Tokens.lxBar; }
  {Eol}              {
                       yy_pop_state(); 
                       Error(89, TokenSpan()); 
                       return (int)Tokens.csOp; 
                     }
  \{                 { 
                       yy_pop_state(); 
                       yy_push_state(BCODE); 
                       depth = 0; 
                       return (int)Tokens.lxLBrace; 
                     }
  {NotWh}            { 
                       yy_pop_state(); 
                       yy_push_state(LCODE); 
                       yyless(0); 
                     }
}

/* Catch-all EOL actions for productions not otherwise defined */
<*>{Eol}             { ResetBadCount(); return (int)Tokens.EOL; }

/* Catch all non-whitespace not part of any other token */
<*>{NotWh}           { Error79(TokenSpan()); return (int)Tokens.repErr; }
/* End of rules. */

%{
    /* Epilog from LEX file */
	yylloc = new LexSpan(tokLin, tokCol, tokELin, tokECol, tokPos, tokEPos, buffer);
%}

// =============================================================
%% // Start of user code
// =============================================================

  /*  User code is in ParseHelper.cs  */

// =============================================================
