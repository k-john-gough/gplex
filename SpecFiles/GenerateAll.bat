REM generate resource resX file and copy to destination
csc GenerateResource.cs
GenerateResource.exe
move Content.resx ..\IncludeResources
del GenerateResource.exe

REM generate a fresh copy of parser.cs
gppg /gplex /nolines gplex.y
move parser.cs ..\GPLEX

REM generate a fresh copy of Scanner.cs
gplex gplex.lex
move Scanner.cs ..\GPLEX

if not exist GplexBuffers.cs goto finish
move GplexBuffers.cs ..\GPLEX

:finish
REM Ended

