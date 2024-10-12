namespace MiniSAT
{
    // NOTE! Variables are just integers. No abstraction here. They should be chosen from 0..N,
    // so that they can be used as array indices.
    using Var = System.Int32;

    public struct Lit
    {
        private const int var_Undef = -1;

        static public Lit lit_Undef = new(var_Undef, false);  // \- Useful special constants.
                                                              //static Lit lit_Error = new Lit(var_Undef, true );  // /

        public int x;

        //TODO we cannot do that, is that a problem?
        //public Lit() : x(2*var_Undef) {}   // (lit_Undef)
        public Lit(Var var, bool sign)
        {
            this.x = var + var + (sign ? 1 : 0);
        }

        public Lit(Var var)
        {
            this.x = var + var;
        }

        public readonly bool Sign => (this.x & 1) != 0;

        public readonly int Var => this.x >> 1;

        public readonly int Index => this.x;

        // A "toInt" method that guarantees small, positive integers suitable for array indexing.
        //static  Lit  toLit (int i) { Lit p = new Lit(); p.x = i; return p; }  // Inverse of 'index()'.
        //static  Lit  unsign(Lit p) { Lit q = new Lit(); q.x = p.x & ~1; return q; }
        //static  Lit  id    (Lit p, bool sgn) { Lit q; q.x = p.x ^ (sgn ? 1 : 0); return q; }

        public static Lit operator ~(Lit p) { Lit q; q.x = p.x ^ 1; return q; }
        public static bool operator ==(Lit p, Lit q) { return p.Index == q.Index; }
        public static bool operator !=(Lit p, Lit q) { return p.Index != q.Index; }
        public static bool operator <(Lit p, Lit q) => p.Index < q.Index; // `<` guarantees that p, ~p are adjacent in the ordering
        public static bool operator >(Lit p, Lit q) => p.Index > q.Index; // Required to be implemented since `<` is defined

        // public static Fraction operator +(Fraction a) => a;

        public override readonly string ToString()
        {
            return (this.Sign ? "-" : "") + "x" + this.Var;
        }

        public override readonly int GetHashCode()
        {
            return this.x;
        }

        public override readonly bool Equals(object other)
        {
            if (other == null)
            {
                return false;
            }


            if (other is Lit)
            {

                return (Lit)other == this;
            }


            return false;
        }
    }
}