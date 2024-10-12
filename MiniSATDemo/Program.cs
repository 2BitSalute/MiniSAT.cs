using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;

using MiniSatCS;

internal class Program
{
    private static string ReadWord(StreamReader s)
    {
        StringBuilder sb = new();
        for (; ; )
        {
            int ch = s.Read();
            if (ch == -1)
            {
                break;
            }


            char c = (char)ch;
            if (char.IsWhiteSpace(c))
            {
                if (sb.Length > 0)
                {
                    break;
                }

            }
            else
            {
                if (c == 'p' || c == 'c')
                {
                    do
                    {
                        ch = s.Read();
                    } while (ch != -1 && (char)ch != '\n');
                    if (sb.Length > 0)
                    {
                        break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
        }

        if (sb.Length == 0)
        {
            return null;
        }
        else
        {
            return sb.ToString();
        }

    }

    public static void Main(string[] args)
    {
        // CNF format briefly explained here:
        //      https://dwheeler.com/essays/minisat-user-guide.html
        // Examples here:
        //      https://people.sc.fsu.edu/~jburkardt/data/cnf/cnf.html
        if (args.Length < 1)
        {
            Console.Error.WriteLine("usage: minisat [-s|-u] <file1.cnf> ...");
            return;
        }

        bool expect = false;
        bool expect_res = false;


        int pos = 0;
        if (args[pos] == "-s")
        {
            expect = true;
            expect_res = true;
            pos++;
        }
        if (args[pos] == "-u")
        {
            expect = true;
            expect_res = false;
            pos++;
        }

        for (; pos < args.Length; pos++)
        {
            StreamReader sr = File.OpenText(args[pos]);
            Vec<Solver.Lit> lits = new();

            Solver S = new();

            for (; ; )
            {
                lits.Clear();
                string w;
                while ((w = ReadWord(sr)) != null)
                {
                    if (w == "%")
                    {
                        break;
                    }


                    int parsed_lit = int.Parse(w);
                    if (parsed_lit == 0)
                    {
                        break;
                    }

                    int var = Math.Abs(parsed_lit) - 1;
                    while (var >= S.nVars())
                    {
                        S.newVar();
                    }


                    lits.Push((parsed_lit > 0) ? new Solver.Lit(var) : ~new Solver.Lit(var));
                }
                if (w == null)
                {
                    break;
                }


                S.addClause(lits);
            }

            if (expect)
            {
                S.verbosity = 0;
                S.solve();
                if (S.okay() == expect_res)
                {
                    Solver.reportf(".");
                }
                else
                {
                    Solver.reportf("\nproblem: {0}\n", args[pos]);
                }

            }
            else
            {
                S.verbosity = 1;
                S.solve();
                Solver.reportf(S.okay() ? "SATISFIABLE\n" : "UNSATISFIABLE\n");
                S.printStats();
            }



#if false
            if (S.okay())
            {
                for (int i = 0; i < S.nVars(); i++)
                    if (S.model[i] != l_Undef)
                        Solver.reportf("{0}{1}\n", (S.model[i] == l_True) ? " " : "-", i + 1);
            }
#endif
        }
    }
} // class MiniSat
