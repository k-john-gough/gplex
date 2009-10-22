//
// Take care if you edit this file.
// The very cut down frame file guesser.frame
// relies on the automaton having no backup states.
// Always check for this property if you edit!
//

%namespace Guesser
%option frame:guesser.frame out:guesser.incl noparser classes nocompress

/* 
 *  Reads the bytes of a file to determine if it is 
 *  UTF-8 or a single-byte codepage file.
 */
 
%{
    public long utfX = 0;
    public long uppr = 0;
%}
 
Utf8pfx2    [\xc0-\xdf]
Utf8pfx3    [\xe0-\xef]
Utf8pfx4    [\xf0-\xf7]
Utf8cont    [\x80-\xbf]
Upper128    [\x80-\xff]
 
%%

{Utf8pfx2}{Utf8cont}     { utfX++; }
{Utf8pfx3}{Utf8cont}{2}  { utfX += 2; }
{Utf8pfx3}{Utf8cont}     { uppr += 2; }
{Utf8pfx4}{Utf8cont}{3}  { utfX += 3; }
{Utf8pfx4}{Utf8cont}     { uppr += 2; }
{Utf8pfx4}{Utf8cont}{2}  { uppr += 3; }
{Upper128}               { uppr++; }
<<EOF>>                  {
                           if (utfX == 0 && uppr == 0) return -1; /* raw ascii */
                           else if (uppr * 10 > utfX) return 0;   /* default codepage */
                           else return 65001;                     /* UTF-8 encoding */
                         }
%%
