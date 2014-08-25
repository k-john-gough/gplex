// Gardens Point Scanner Generator
// Copyright (c) K John Gough, QUT 2006-2014
// (see accompanying GPLEXcopyright.rtf)

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using QUT.Gplex.Automaton;
using QUT.Gplex.Parser;

[assembly: CLSCompliant(true)]
namespace QUT.Gplex
{
	static class Program
	{
		const string prefix = "GPLEX: ";

		static void Main(string[] args)
		{
            bool fileArg = false;
			TaskState task = new TaskState();
            OptionState opResult = OptionState.clear;
			if (args.Length == 0)
				Usage("No arguments");
			for (int i = 0; i < args.Length; i++)
			{
                if (args[i][0] == '/' || args[i][0] == '-')
                {
                    string arg = args[i].Substring(1);
                    opResult = task.ParseOption(arg);
                    if (opResult != OptionState.clear &&
                        opResult != OptionState.needCodepageHelp &&
                        opResult != OptionState.needUsage)
                        BadOption(arg, opResult);
                }
                else if (i != args.Length - 1)
                    Usage("Too many arguments");
                else
                    fileArg = true;
			}
            if (task.Version)
                task.Msg.WriteLine("GPLEX version: " + task.VerString);
            if (opResult == OptionState.needCodepageHelp)
                CodepageHelp(fileArg);
            if (opResult == OptionState.errors)
                Usage(null); // print usage and abort
            else if (!fileArg)
                Usage("No filename");
            else if (opResult == OptionState.needUsage)
                Usage();     // print usage but do not abort
            try
            {
                task.Process(args[args.Length - 1]);
            }
            catch (Exception ex)
            {
                if (ex is TooManyErrorsException)
                    return;
                Console.Error.WriteLine(ex.Message);
                throw;
            }
            finally
            {
                if (task.ErrNum + task.WrnNum > 0 || task.Listing)
                    task.MakeListing();
                if (task.ErrNum + task.WrnNum > 0)
                    task.ErrorReport();
                else if (task.Verbose)
                    task.Msg.WriteLine("GPLEX <" + task.FileName + "> Completed successfully");
                task.Dispose();
            }
            if (task.ErrNum > 0)
                Environment.Exit(1);
            else
                Environment.Exit(0);
		}

		static void BadOption(string arg, OptionState rslt)
		{
            string marker = "";
            switch (rslt)
            {
                case OptionState.errors:
                    marker = "unknown argument";
                    break;
                case OptionState.inconsistent:
                    marker = "inconsistent argument";
                    break;
                case OptionState.alphabetLocked:
                    marker = "can't change alphabet";
                    break;
            }
            Console.Error.WriteLine("{0} {1}: {2}", prefix, marker, arg);
		}

		static void Usage() // print the usage message but do not abort.
		{
			Console.WriteLine(prefix + "Usage");
			Console.WriteLine("gplex [options] filename");
			Console.WriteLine("default filename extension is \".lex\"");
  			Console.WriteLine("  options:  /babel           -- create extra interface for Managed Babel scanner");
            Console.WriteLine("            /caseInsensitive -- create a case-insensitive automaton");
            Console.WriteLine("            /check           -- create automaton but do not create output file");
            Console.WriteLine("            /codePage:NN     -- default codepage NN if no unicode prefix (BOM)");
            Console.WriteLine("            /codePageHelp    -- display codepage help");
            Console.WriteLine("            /classes         -- use character equivalence classes");
            Console.WriteLine("            /errorsToConsole -- legacy error-messages (not MSBUILD friendly)");
            Console.WriteLine("            /frame:path      -- use \"path\" as frame file" );
            Console.WriteLine("            /help            -- display this usage message" );
            Console.WriteLine("            /info            -- scanner has header comment (on by default)");
            Console.WriteLine("            /listing         -- emit listing even if no errors");
            Console.WriteLine("            /noCompress      -- do not compress scanner tables");
            Console.WriteLine("            /noCompressMap   -- do not compress character map");
            Console.WriteLine("            /noCompressNext  -- do not compress nextstate tables");
            Console.WriteLine("            /noFiles         -- no file input buffers, string input only");
            Console.WriteLine("            /noMinimize      -- do not minimize the states of the dfsa");
            Console.WriteLine("            /noParser        -- create stand-alone scanner");
            Console.WriteLine("            /noPersistBuffer -- do not retain input buffer throughout processing");
            Console.WriteLine("            /noEmbedBuffers  -- write buffers to separate GplexBuffers file");
            Console.WriteLine("            /out:path        -- send output to filename \"path\"");
            Console.WriteLine("            /out:-           -- send output to Console.Out");
            Console.WriteLine("            /parseOnly       -- syntax check only, do not create automaton");
            Console.WriteLine("            /stack           -- enable built-in stacking of start states");
            Console.WriteLine("            /squeeze         -- sacrifice speed for small size");
            Console.WriteLine("            /summary         -- emit statistics to list file");
            Console.WriteLine("            /unicode         -- generate a unicode enabled scanner");
            Console.WriteLine("            /verbose         -- chatter on about progress");
            Console.WriteLine("            /version         -- give version information for GPLEX");
        }

        static void CodepageHelp(bool hasfile)
        {
            Console.WriteLine(prefix + "CodePage Help");
            Console.WriteLine("CodePage options define the fallback codepage for unicode scanners if");
            Console.WriteLine("an input file does not start with a valid UTF prefix / byte order mark.");
            Console.WriteLine("  options:  /codePage:nmbr    -- fallback to codepage with number nmbr");
            Console.WriteLine("            /codePage:idnt    -- fallback to codepage with name idnt");
            Console.WriteLine("            /codePage:default -- fallback to default codepage of host machine");
            Console.WriteLine("            /codePage:guess   -- scan the file to identify probable codepage");
            Console.WriteLine("            /codePage:raw     -- use the raw bytes from the file");
            Console.WriteLine("For byte-mode scanners, codePage options define the mapping of input");
            Console.WriteLine("bytes to unicode for character class predicates such as \"[:IsLetter:]\".");
            Console.WriteLine("  options:  /codePage:nmbr    -- map to unicode using codepage number nmbr");
            Console.WriteLine("            /codePage:idnt    -- map to unicode using codepage with name idnt");
            Console.WriteLine("            /codePage:default -- map using default codepage of host machine");
            Console.WriteLine("            /codePage:raw     -- do not use any codepage mapping");
            if (!hasfile)
            {
                Console.WriteLine(prefix + "No input file, terminating ...");
                Environment.Exit(1);
            }
        }

		static void Usage(string msg)  // print the usage message and die.
		{
			if (msg != null)
				Console.WriteLine(prefix + msg);
			Usage();
			Console.WriteLine("  Terminating ...");
			Environment.Exit(1);
		}
	}
}
