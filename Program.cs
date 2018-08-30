using System;
using System.IO;
using SIL.Scripture;

namespace MarkerCheck
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2 || args.Length > 3)
                BadArguments();

            if (args.Length == 3 && args[0] != "-usfm2")
                BadArguments();

            int bookArg = args.Length == 2 ? 0 : 1;
            int bookNum = Canon.BookIdToNumber(args[bookArg]);
            if (bookNum <= 0)
                BadArguments();

            string textFile = args[bookArg + 1];
            if (!File.Exists(textFile))
                BadArguments();

            // allow USFM 3 markers to be used if not turned off
            MarkerCheck check = new MarkerCheck(args.Length == 2);
            if (check.Run(bookNum, File.ReadAllText(textFile)))
            {
                Console.WriteLine("MarkerCheck: file had errors");
                Environment.Exit(1);
            }
        }

        private static void BadArguments()
        {
            Console.WriteLine("Unexpected arguments.");
            Console.WriteLine("MarkerCheck [-usfm2] BookCode UsfmTextFileName");
            Environment.Exit(1);
        }
    }
}
