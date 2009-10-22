/*
 *  Parser spec for GPLEX
 *  Process with > GPPG /gplex /no-lines gplex.y
 */
 
%using System.Collections
%namespace QUT.Gplex.Parser

%YYLTYPE LexSpan

%partial
%visibility internal
%output=Parser.cs

%token csKeyword csIdent csNumber csLitstr csLitchr csOp 
       csBar "|", csDot ".", semi ";", csStar "*", csLT "<", csGT ">", 
       comma ",", csSlash "/", csLBrac "[", csRBrac "]", csLPar "(",
       csRPar ")", csLBrace "{", csRBrace "}"
%token verbatim pattern name lCond "<", rCond ">", 
       lxLBrace "{", lxRBrace "}", lxBar "|", defCommentS "/*", 
       defCommentE "/*...*/", csCommentS "/*", csCommentE "/*...*/"
%token usingTag "%using", namespaceTag "%namespace", optionTag "%option",
       charSetPredTag "%charClassPredicate", inclTag "%s", exclTag "%x",
       lPcBrace "%{", rPcBrace "%}", visibilityTag "%visibility", PCPC "%%", 
       userCharPredTag "userCharPredicate", scanbaseTag "%scanbasetype", 
       tokentypeTag "%tokentype", scannertypeTag "scannertype",
       lxIndent lxEndIndent 

%token maxParseToken EOL csCommentL errTok repErr
  
%%

Program
    : DefinitionSection Rules
    | DefinitionSection RulesSection UserCodeSection
    ;
    
DefinitionSection
    : DefinitionSeq  PCPC    {
                       isBar = false; 
                       typedeclOK = false;
                       if (aast.nameString == null) 
                           handler.ListError(@2, 73); 
                     }
    | PCPC           { isBar = false; }
    | error PCPC     { handler.ListError(@1, 62, "%%"); }
    ;
 
RulesSection
    : Rules PCPC     { typedeclOK = true; }
    ;
    
UserCodeSection
    : CSharp         { aast.UserCode = @1; }
    | /* empty */    {  /* empty */  }
    | error          { handler.ListError(@1, 62, "EOF"); }
    ;
    
DefinitionSeq
    : DefinitionSeq Definition
    | Definition
    ;

Definition
    : name verbatim            { AddLexCategory(@1, @2); }
    | name pattern             { AddLexCategory(@1, @2); }
    | exclTag NameList         { AddNames(true); }
    | inclTag NameList         { AddNames(false); }
    | usingTag DottedName semi { aast.usingStrs.Add(@2.Merge(@3)); }
    | namespaceTag DottedName  { aast.nameString = @2; }
    | visibilityTag csKeyword  { aast.AddVisibility(@2); }
    | optionTag verbatim       { ParseOption(@2); }
    | PcBraceSection           { aast.AddCodeSpan(Dest,@1); }
    | DefComment               { aast.AddCodeSpan(Dest,@1); }
    | IndentedCode             { aast.AddCodeSpan(Dest,@1); }
    | charSetPredTag NameList  { AddCharSetPredicates(); }
    | scanbaseTag csIdent      { aast.SetScanBaseName(@2.ToString()); }
    | tokentypeTag csIdent     { aast.SetTokenTypeName(@2.ToString()); }
    | scannertypeTag csIdent   { aast.SetScannerTypeName(@2.ToString()); }
    | userCharPredTag csIdent csLBrac DottedName csRBrac DottedName {
                                 aast.AddUserPredicate(@2.ToString(), @4, @6);
                               }  
    ;
    

IndentedCode
    : lxIndent CSharp lxEndIndent  { @$ = @2; }
    | lxIndent error lxEndIndent   { handler.ListError(@2, 64); }
    ;

NameList
    : NameSeq
    | error                    { handler.ListError(@1, 67); }
    ;

NameSeq
    : NameSeq comma name       { AddName(@3); }
    | name                     { AddName(@1); }
    | csNumber                 { AddName(@1); }
    | NameSeq comma error      { handler.ListError(@2, 67); }
    ;

PcBraceSection
    : lPcBrace rPcBrace        { @$ = BlankSpan; /* skip blank lines */ }
    | lPcBrace CSharpN rPcBrace  { 
                                 @$ = @2; 
                               }
    | lPcBrace error rPcBrace  { handler.ListError(@2, 62, "%}"); }
    | lPcBrace error PCPC      { handler.ListError(@2, 62, "%%"); }
    ;
    

DefComment
    : DefComStart CommentEnd
    | defCommentE                     
    ;
    
CommentEnd
    : defCommentE
    | csCommentE
    ;

DefComStart
    : DefComStart defCommentS
    | DefComStart csCommentS
    | defCommentS                     
    ;
    
BlockComment
    : CsComStart CommentEnd
    ;

CsComStart
    : CsComStart csCommentS 
    | CsComStart defCommentS 
    | csCommentS                      
    ;
    
Rules
    : RuleList                { 
                                rb.FinalizeCode(aast); 
                                aast.FixupBarActions(); 
                              }
    | /* empty */ 
    ;
    
RuleList
    : RuleList Rule
    | Rule
    ;
    
Rule
    : Production
    | ProductionGroup         { scope.ClearScope(); /* for error recovery */ }
    | PcBraceSection          { rb.AddSpan(@1); }
    | IndentedCode            { rb.AddSpan(@1); }
    | BlockComment            { /* ignore */ }
    | csCommentE              { /* ignore */ }
    ;
    
Production
    :  ARule                  {
			                    int thisLine = @1.startLine;
			                    rb.LLine = thisLine;
			                    if (rb.FLine == 0) rb.FLine = thisLine;
		                      }
    ;
    
ProductionGroup
    : StartCondition lxLBrace PatActionList lxRBrace {
                                scope.ExitScope();
                              }
    ;
    
PatActionList
    : /* empty */             { 
                                int thisLine = @$.startLine;
			                    rb.LLine = thisLine;
			                    if (rb.FLine == 0) 
			                        rb.FLine = thisLine;
			                  }
    | PatActionList ARule   
    | PatActionList ProductionGroup
    ;
    
ARule                                          
    : StartCondition pattern Action  {
			                    RuleDesc rule = new RuleDesc(@2, @3, scope.Current, isBar);
			                    aast.ruleList.Add(rule);
			                    rule.ParseRE(aast);
			                    isBar = false; // Reset the flag ...
			                    scope.ExitScope();
		                      }
		                      
    | pattern Action          {
			                    RuleDesc rule = new RuleDesc(@1, @2, scope.Current, isBar); 
			                    aast.ruleList.Add(rule);
			                    rule.ParseRE(aast); 
			                    isBar = false; // Reset the flag ...
		                      }
		                      
    | error                   { handler.ListError(@1, 68); scope.ClearScope(); }
    ;
    
StartCondition
    : lCond NameList rCond    { 
                                List<StartState> list =  new List<StartState>();
                                AddNameListToStateList(list);
                                scope.EnterScope(list); 
                              }
    | lCond csStar rCond      {
                                List<StartState> list =  new List<StartState>(); 
                                list.Add(StartState.allState);
                                scope.EnterScope(list); 
                              }
    ;

CSharp
    : CSharpN                      
    ;

CSharpN
    : NonPairedToken
    | CSharpN NonPairedToken
    | CSharpN WFCSharpN
    | WFCSharpN                     
    ;

WFCSharpN
    /* : NonPairedToken */
    : csLBrace csRBrace
    | csLBrace CSharpN csRBrace
    | csLPar csRPar
    | csLPar CSharpN csRPar
    | csLBrac csRBrac
    | csLBrac CSharpN csRBrac
    | csLPar error             { handler.ListError(@2, 61, "')'"); }
    | csLBrac error            { handler.ListError(@2, 61, "']'"); }
    | csLBrace error           { handler.ListError(@2, 61, "'}'"); }
    ;
    
DottedName
    : csIdent                  { /* skip1 */ }
    | csKeyword csDot csIdent  { /* skip2 */ }
    | DottedName csDot csIdent { /* skip3 */ }
    ;

NonPairedToken
    : BlockComment                     
    | DottedName                              
    | csCommentE                       
    | csCommentL                       
    | csKeyword                { 
                                 string text = aast.scanner.yytext;
                                 if (text.Equals("using")) {
                                     handler.ListError(@1, 56);
                                 } else if (text.Equals("namespace")) {
                                     handler.ListError(@1, 57);
                                 } else {
                                     if ((text.Equals("class") || text.Equals("struct") ||
                                          text.Equals("enum")) && !typedeclOK) handler.ListError(@1,58);
                                 }
                               }
    | csNumber                             
    | csLitstr                             
    | csLitchr                             
    | csOp
    | csDot
    | csStar                                
    | csLT                                  
    | csGT
    | semi                                 
    | comma                                 
    | csSlash                               
    | csBar                                 
    ;
    
Action
    : lxLBrace CSharp lxRBrace { @$ = @2; }
    | CSharp                           
    | lxBar                    { isBar = true; }
    | lxLBrace lxRBrace
    | lxLBrace error lxRBrace  { handler.ListError(@$, 65); }
    | error                    { handler.ListError(@1, 63); }
    ;
    
%%
