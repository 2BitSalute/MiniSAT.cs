namespace MiniSAT.Utils
{
    using System;
    using System.Diagnostics;

    public class Common
    {
        [Conditional("DEBUG")]
        static public void Assert(bool expr)
        {
            if (!expr)
            {

                throw new Exception("assertion violated");
            }
        }

        /// <summary>
        /// Just like 'assert()' but expression will be evaluated in the release version as well.
        /// </summary>
        /// <param name="expr">What to evaluate</param>
        public static void Check(bool expr) { Assert(expr); }

        //=================================================================================================
        // Random numbers:

        // Returns a random float 0 <= x < 1. Seed must never be 0.
        public static double drand(ref double seed)
        {
            seed *= 1389796;
            int q = (int)(seed / 2147483647);
            seed -= (double)q * 2147483647;
            return seed / 2147483647;
        }

        // Returns a random integer 0 <= x < size. Seed must never be 0.
        public static int irand(ref double seed, int size)
        {
            return (int)(drand(ref seed) * size);
        }
    }
}