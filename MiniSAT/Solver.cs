/******************************************************************************************
MiniSat -- Copyright (c) 2003-2005, Niklas Een, Niklas Sorensson
MiniSatCS -- Copyright (c) 2006-2007 Michal Moskal

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
associated documentation files (the "Software"), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute,
sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT
NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT
OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
**************************************************************************************************/



namespace MiniSAT
{

    using System;
    using System.IO;
    using System.Text;
    using System.Diagnostics;
    using System.Collections.Generic;

    // NOTE! Variables are just integers. No abstraction here. They should be chosen from 0..N,
    // so that they can be used as array indices.
    using Var = System.Int32;
    using MiniSAT.DataStructures;
    using MiniSAT.Utils;

    public class Solver
    {
        #region Literals
        //=================================================================================================
        // Variables, literals, clause IDs:

        private const int var_Undef = -1;

        public struct Lit
        {
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

            public static Lit operator ~(Lit p) { Lit q; q.x = p.x ^ 1; return q; }
            public static bool operator ==(Lit p, Lit q) { return index(p) == index(q); }
            public static bool operator !=(Lit p, Lit q) { return index(p) != index(q); }
            public static bool operator <(Lit p, Lit q) => index(p) < index(q); // `<` guarantees that p, ~p are adjacent in the ordering
            public static bool operator >(Lit p, Lit q) => index(p) > index(q); // Required to be implemented since `<` is defined

            // public static Fraction operator +(Fraction a) => a;

            public override string ToString()
            {
                return (sign(this) ? "-" : "") + "x" + var(this);
            }

            public override int GetHashCode()
            {
                return this.x;
            }

            public override bool Equals(object other)
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


        static public bool sign(Lit p) { return (p.x & 1) != 0; }
        static public int var(Lit p) { return p.x >> 1; }
        static public int index(Lit p) { return p.x; }                // A "toInt" method that guarantees small, positive integers suitable for array indexing.
                                                                      //static  Lit  toLit (int i) { Lit p = new Lit(); p.x = i; return p; }  // Inverse of 'index()'.
                                                                      //static  Lit  unsign(Lit p) { Lit q = new Lit(); q.x = p.x & ~1; return q; }
                                                                      //static  Lit  id    (Lit p, bool sgn) { Lit q; q.x = p.x ^ (sgn ? 1 : 0); return q; }

        static public Lit lit_Undef = new(var_Undef, false);  // \- Useful special constants.
                                                              //static Lit lit_Error = new Lit(var_Undef, true );  // /
        #endregion

        #region Clauses
        //=================================================================================================
        // Clause -- a simple class for representing a clause:


        private const int ClauseId_null = int.MinValue;

        //- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -

        public class Clause
        {
            private Lit[] data;
            private bool is_learnt;
            protected internal Clause(bool learnt, Vec<Lit> ps)
            {
                this.is_learnt = learnt;
                this.data = new Lit[ps.Size()];
                for (int i = 0; i < ps.Size(); i++)
                {
                    this.data[i] = ps[i];
                }

            }


            public int size() { return this.data.Length; }
            public bool learnt() { return this.is_learnt; }

            public Lit this[int i]
            {
                get { return this.data[i]; }
                set { this.data[i] = value; }
            }

            public float activity;

            public override string ToString()
            {
                StringBuilder sb = new();
                sb.Append("[");
                foreach (Lit l in this.data)
                {
                    sb.Append(l).Append(", ");
                }


                sb.Append("]");
                return sb.ToString();
            }

            public Lit[] GetData() { return this.data; }
        }

        protected virtual Clause Clause_new(bool learnt, Vec<Lit> ps)
        {
            return new Clause(learnt, ps);
        }

        #endregion

        #region Utilities

        //=================================================================================================
        // Time and Memory:

        private static double cpuTime()
        {
            return (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
        }

        private static long memUsed()
        {
            return GC.GetTotalMemory(false);
        }

        // Redfine if you want output to go somewhere else:
        public static void reportf(string format, params object[] args)
        {
            System.Console.Write(format, args);
        }
        public static void debug(string format, params object[] args)
        {
            System.Console.WriteLine(format, args);
        }
        #endregion

        #region Stats, params
        public class SolverStats
        {
            public long starts, decisions, propagations, conflicts;
            public long clauses_literals, learnts_literals, max_literals, tot_literals;
        }


        public class SearchParams
        {
            public double var_decay, clause_decay, random_var_freq;    // (reasonable values are: 0.95, 0.999, 0.02)    
            public SearchParams() : this(1, 1, 0) { }
            public SearchParams(SearchParams other) : this(other.var_decay, other.clause_decay, other.random_var_freq) { }
            public SearchParams(double v, double c, double r)
            {
                this.var_decay = v; this.clause_decay = c;
                this.random_var_freq = r;
            }
        }
        #endregion

        #region Solver state
        private bool ok;               // If FALSE, the constraints are already unsatisfiable. No part of the solver state may be used!
        protected Vec<Clause> clauses;          // List of problem clauses.
        protected Vec<Clause> learnts;          // List of learnt clauses.
        private double cla_inc;          // Amount to bump next clause with.
        private double cla_decay;        // INVERSE decay factor for clause activity: stores 1/decay.

        public Vec<double> activity;         // A heuristic measurement of the activity of a variable.
        private double var_inc;          // Amount to bump next variable with.
        private double var_decay;        // INVERSE decay factor for variable activity: stores 1/decay. Use negative value for static variable order.
        private VarOrder order;            // Keeps track of the decision variable order.

        private Vec<Vec<Clause>> watches;          // 'watches[lit]' is a list of constraints watching 'lit' (will go there if literal becomes true).
        public Vec<LiftedBool.Value> assigns;          // The current assignments.
        public Vec<Lit> trail;            // Assignment stack; stores all assigments made in the order they were made.
        protected Vec<int> trail_lim;        // Separator indices for different decision levels in 'trail'.
        protected Vec<Clause> reason;           // 'reason[var]' is the clause that implied the variables current value, or 'null' if none.
        protected Vec<int> level;            // 'level[var]' is the decision level at which assignment was made.
        private Vec<int> trail_pos;        // 'trail_pos[var]' is the variable's position in 'trail[]'. This supersedes 'level[]' in some sense, and 'level[]' will probably be removed in future releases.
        private int root_level;       // Level of first proper decision.
        private int qhead;            // Head of queue (as index into the trail -- no more explicit propagation queue in MiniSat).
        private int simpDB_assigns;   // Number of top-level assignments since last execution of 'simplifyDB()'.
        private long simpDB_props;     // Remaining number of propagations that must be made before next execution of 'simplifyDB()'.

        // Temporaries (to reduce allocation overhead). Each variable is prefixed by the method in which is used:
        //
        private Vec<LiftedBool.Value> analyze_seen;
        private Vec<Lit> analyze_stack;
        private Vec<Lit> analyze_toclear;
        private Vec<Lit> addUnit_tmp;
        private Vec<Lit> addBinary_tmp;
        private Vec<Lit> addTernary_tmp;
        #endregion

        #region Main internal methods:
        private void analyzeFinal(Clause confl) { this.analyzeFinal(confl, false); }

        private bool enqueue(Lit fact) { return this.enqueue(fact, null); }

        // Activity:
        //
        private void varBumpActivity(Lit p)
        {
            if (this.var_decay < 0)
            {
                return;     // (negative decay means static variable order -- don't bump)
            }


            if ((this.activity[var(p)] += this.var_inc) > 1e100)
            {
                this.varRescaleActivity();
            }


            this.order.update(var(p));
        }

        private void varDecayActivity()
        {
            if (this.var_decay >= 0)
            {
                this.var_inc *= this.var_decay;
            }
        }

        private void claDecayActivity() { this.cla_inc *= this.cla_decay; }

        // Operations on clauses:
        //
        private void newClause(Vec<Lit> ps) { this.newClause(ps, false); }

        private void claBumpActivity(Clause c)
        {
            if ((c.activity += (float)this.cla_inc) > 1e20)
            {
                this.claRescaleActivity();
            }
        }
        protected void remove(Clause c) { this.remove(c, false); }
        protected bool locked(Clause c) { return c == this.reason[var(c[0])]; }

        private int decisionLevel() { return this.trail_lim.Size(); }
        #endregion

        #region Public interface
        public Solver()
        {
            this.clauses = new Vec<Clause>();
            this.learnts = new Vec<Clause>();
            this.activity = new Vec<double>();
            this.watches = new Vec<Vec<Clause>>();
            this.assigns = new Vec<LiftedBool.Value>();
            this.trail_pos = new Vec<int>();
            this.trail = new Vec<Lit>();
            this.trail_lim = new Vec<int>();
            this.reason = new Vec<Clause>();
            this.level = new Vec<int>();
            this.analyze_seen = new Vec<LiftedBool.Value>();
            this.analyze_stack = new Vec<Lit>();
            this.analyze_toclear = new Vec<Lit>();
            this.addUnit_tmp = new Vec<Lit>();
            this.addBinary_tmp = new Vec<Lit>();
            this.addTernary_tmp = new Vec<Lit>();
            this.model = new Vec<LiftedBool.Value>();
            this.conflict = new Vec<Lit>();
            this.addUnit_tmp.GrowTo(2);
            this.addBinary_tmp.GrowTo(2);
            this.addTernary_tmp.GrowTo(3);

            this.stats = new SolverStats();

            this.ok = true;
            this.cla_inc = 1;
            this.cla_decay = 1;
            this.var_inc = 1;
            this.var_decay = 1;
            this.order = this.createOrder();
            this.qhead = 0;
            this.simpDB_assigns = 0;
            this.simpDB_props = 0;
            this.default_parms = new SearchParams(0.95, 0.999, 0.02);
            this.expensive_ccmin = true;
            this.verbosity = 0;
            this.progress_estimate = 0;

            Vec<Lit> dummy = new(2, lit_Undef);
            dummy.Pop();

        }

        protected virtual VarOrder createOrder()
        {
            return new VarOrder(this.assigns, this.activity, this.);
        }

        ~Solver()
        {
            for (int i = 0; i < this.learnts.Size(); i++)
            {
                this.remove(this.learnts[i], true);
            }

            for (int i = 0; i < this.clauses.Size(); i++)
            {
                if (this.clauses[i] != null)
                {
                    this.remove(this.clauses[i], true);
                }
            }
        }

        // Helpers: (semi-internal)
        //
        public LiftedBool.Value value(Var x) { return this.assigns[x]; }
        public LiftedBool.Value value(Lit p) { return sign(p) ? ~this.assigns[var(p)] : this.assigns[var(p)]; }

        public int nAssigns() { return this.trail.Size(); }
        public int nClauses() { return this.clauses.Size(); }   // (minor difference from MiniSat without the GClause trick: learnt binary clauses will be counted as original clauses)
        public int nLearnts() { return this.learnts.Size(); }

        // Statistics: (read-only member variable)
        //
        public SolverStats stats;

        // Mode of operation:
        //
        public SearchParams default_parms;     // Restart frequency etc.
        public bool expensive_ccmin;    // Controls conflict clause minimization. TRUE by default.
        public int verbosity;          // Verbosity level. 0=silent, 1=some progress report, 2=everything

        // Problem specification:
        //
        // public Var     newVar    ();
        public int nVars() { return this.assigns.Size(); }
        public void addUnit(Lit p) { this.addUnit_tmp[0] = p; this.addClause(this.addUnit_tmp); }
        public void addBinary(Lit p, Lit q) { this.addBinary_tmp[0] = p; this.addBinary_tmp[1] = q; this.addClause(this.addBinary_tmp); }
        public void addTernary(Lit p, Lit q, Lit r) { this.addTernary_tmp[0] = p; this.addTernary_tmp[1] = q; this.addTernary_tmp[2] = r; this.addClause(this.addTernary_tmp); }
        public void addClause(Vec<Lit> ps) { this.newClause(ps); }  // (used to be a difference between internal and external method...)

        // Solving:
        //
        public bool okay() { return this.ok; }       // FALSE means solver is in an conflicting state (must never be used again!)
                                                     //public void    simplifyDB();
                                                     //public bool    solve(vec<Lit> assumps);
        public bool solve() { Vec<Lit> tmp = new(); return this.solve(tmp); }

        public double progress_estimate;  // Set by 'search()'.
        public Vec<LiftedBool.Value> model;              // If problem is satisfiable, this vector contains the model (if any).
        public Vec<Lit> conflict;           // If problem is unsatisfiable (possibly under assumptions), this vector represent the conflict clause expressed in the assumptions.
        #endregion

        #region Operations on clauses:
        /*_________________________________________________________________________________________________
        |
        |  newClause : (ps : const vec<Lit>&) (learnt : bool)  .  [void]
        |  
        |  Description:
        |    Allocate and add a new clause to the SAT solvers clause database. If a conflict is detected,
        |    the 'ok' flag is cleared and the solver is in an unusable state (must be disposed).
        |  
        |  Input:
        |    ps     - The new clause as a vector of literals.
        |    learnt - Is the clause a learnt clause? For learnt clauses, 'ps[0]' is assumed to be the
        |             asserting literal. An appropriate 'enqueue()' operation will be performed on this
        |             literal. One of the watches will always be on this literal, the other will be set to
        |             the literal with the highest decision level.
        |  
        |  Effect:
        |    Activity heuristics are updated.
        |________________________________________________________________________________________________@*/
        private Vec<Lit> BasicClauseSimplification(Vec<Lit> ps, bool copy)
        {
            Vec<Lit> qs;
            if (copy)
            {
                qs = new Vec<Lit>();
                ps.CopyTo(qs);             // Make a copy of the input vector.
            }
            else
            {
                qs = ps;
            }

            Dictionary<Var, Lit> dict = new(ps.Size());
            int ptr = 0;

            for (int i = 0; i < qs.Size(); i++)
            {
                Lit l = qs[i];
                Lit other;
                Var v = var(l);
                if (dict.TryGetValue(v, out other))
                {
                    if (other == l) { } // already seen it
                    else
                    {
                        return null; // other = ~l, so always satisfied
                    }

                }
                else
                {
                    dict[v] = l;
                    qs[ptr++] = l;
                }
            }
            qs.ShrinkTo(ptr);

            return qs;
        }

        private void reorderByLevel(Vec<Lit> ps)
        {
            int max = int.MinValue;
            int max_at = -1;
            int max2 = int.MinValue;
            int max2_at = -1;
            for (int i = 0; i < ps.Size(); ++i)
            {
                int lev = this.level[var(ps[i])];
                if (lev == -1)
                {
                    lev = int.MaxValue;
                }
                else if (this.value(ps[i]) == LiftedBool.Value.True)
                {
                    lev = int.MaxValue;
                }


                if (lev >= max)
                {
                    max2_at = max_at;
                    max2 = max;
                    max = lev;
                    max_at = i;
                }
                else if (lev > max2)
                {
                    max2 = lev;
                    max2_at = i;
                }
            }

            if (max_at == 0)
            {
                ps.Swap(1, max2_at);
            }
            else if (max_at == 1)
            {
                ps.Swap(0, max2_at);
            }
            else if (max2_at == 0)
            {
                ps.Swap(1, max_at);
            }
            else if (max2_at == 1)
            {
                ps.Swap(0, max_at);
            }

            else
            {
                ps.Swap(0, max_at);
                ps.Swap(1, max2_at);
            }
        }

        protected void newClause(Vec<Lit> ps_, bool learnt)
        {
            this.newClause(ps_, learnt, false, true);
        }

        protected void newClause(Vec<Lit> ps_, bool learnt, bool theoryClause, bool copy)
        {
            if (!this.ok)
            {
                return;
            }

            //foreach (Lit p in ps_) { Console.Write (" {0} ", p); } Console.WriteLine (" END");

            Vec<Lit> ps;

            Utils.Assert(!(learnt && theoryClause));

            if (!learnt)
            {
                Utils.Assert(theoryClause || this.decisionLevel() == 0);

                Vec<Lit> qs = this.BasicClauseSimplification(ps_, copy);

                if (qs == null)
                {
                    return;
                }

                // Check if clause is satisfied:
                for (int i = 0; i < qs.Size(); i++)
                {
                    if (this.level[var(qs[i])] == 0 && this.value(qs[i]) == LiftedBool.Value.True)
                    {
                        return;
                    }

                }

                // Remove false literals:
                {
                    int i, j;
                    for (i = j = 0; i < qs.Size(); i++)
                    {

                        if (this.level[var(qs[i])] != 0 || this.value(qs[i]) != LiftedBool.Value.False)
                        {
                            qs[j++] = qs[i];
                        }
                    }


                    qs.Shrink(i - j);
                }

                ps = qs;
            }
            else
            {
                ps = ps_;
            }

            // 'ps' is now the (possibly) reduced vector of literals.


            if (ps.Size() == 0)
            {
                this.ok = false;

            }
            else if (ps.Size() == 1)
            {
                // NOTE: If enqueue takes place at root level, the assignment will be lost in incremental use (it doesn't seem to hurt much though).
                //if (!enqueue(ps[0], GClause_new(Clause_new(learnt, ps))))
                if (theoryClause)
                {
                    this.levelToBacktrack = 0;
                    this.cancelUntil(0);
                }

                Clause c = this.Clause_new(learnt || theoryClause, ps);
                this.NewClauseCallback(c);

                if (!this.enqueue(ps[0]))
                {
                    this.ok = false;
                }

            }
            else
            {
                if (theoryClause)
                {
                    this.reorderByLevel(ps);
                }

                // Allocate clause:

                Clause c = this.Clause_new(learnt || theoryClause, ps);

                if (!learnt && !theoryClause)
                {
                    // Store clause:
                    this.clauses.Push(c);
                    this.stats.clauses_literals += c.size();
                }
                else
                {
                    if (learnt)
                    {
                        // Put the second watch on the literal with highest decision level:
                        int max_i = 1;
                        int max = this.level[var(ps[1])];
                        for (int i = 2; i < ps.Size(); i++)
                        {

                            if (this.level[var(ps[i])] > max)
                            {
                                max = this.level[var(ps[i])];
                                max_i = i;
                            }
                        }


                        c[1] = ps[max_i];
                        c[max_i] = ps[1];

                        Utils.Check(this.enqueue(c[0], c));
                    }
                    else
                    {
                        this.MoveBack(c[0], c[1]);
                    }

                    // Bumping:
                    this.claBumpActivity(c);         // (newly learnt clauses should be considered active)
                    this.learnts.Push(c);
                    this.stats.learnts_literals += c.size();
                }


                // Watch clause:
                this.watches[index(~c[0])].Push(c);
                this.watches[index(~c[1])].Push(c);
                this.NewClauseCallback(c);
            }
        }


        // Disposes a clauses and removes it from watcher lists. NOTE! Low-level; does NOT change the 'clauses' and 'learnts' vector.
        //
        private void remove(Clause c, bool just_dealloc)
        {
            if (!just_dealloc)
            {
                removeWatch(this.watches[index(~c[0])], c);
                removeWatch(this.watches[index(~c[1])], c);
            }

            if (c.learnt())
            {
                this.stats.learnts_literals -= c.size();
            }
            else
            {
                this.stats.clauses_literals -= c.size();
            }

            //xfree(c);

        }


        // Can assume everything has been propagated! (esp. the first two literals are != LiftedBool.Value.False, unless
        // the clause is binary and satisfied, in which case the first literal is true)
        // Returns True if clause is satisfied (will be removed), False otherwise.
        //
        private bool simplify(Clause c)
        {
            Utils.Assert(this.decisionLevel() == 0);
            for (int i = 0; i < c.size(); i++)
            {
                if (this.value(c[i]) == LiftedBool.Value.True)
                {

                    return true;
                }

            }
            return false;
        }
        #endregion

        #region Minor methods
        private static bool removeWatch(Vec<Clause> ws, Clause elem)    // Pre-condition: 'elem' must exists in 'ws' OR 'ws' must be empty.
        {
            if (ws.Size() == 0)
            {
                return false;     // (skip lists that are already cleared)
            }


            int j = 0;
            for (; ws[j] != elem; j++)
            {
                Utils.Assert(j < ws.Size() - 1);
            }


            for (; j < ws.Size() - 1; j++)
            {
                ws[j] = ws[j + 1];
            }


            ws.Pop();
            return true;
        }
        // Creates a new SAT variable in the solver. If 'decision_var' is cleared, variable will not be
        // used as a decision variable (NOTE! This has effects on the meaning of a SATISFIABLE result).
        //
        public Var newVar()
        {
            int index;
            index = this.nVars();
            this.watches.Push(new Vec<Clause>());          // (list for positive literal)
            this.watches.Push(new Vec<Clause>());          // (list for negative literal)
            this.reason.Push(null);
            this.assigns.Push(LiftedBool.Value.Undef0);
            this.level.Push(-1);
            this.trail_pos.Push(-1);
            this.activity.Push(0);
            this.order.newVar();
            this.analyze_seen.Push(0);
            return index;
        }


        // Returns FALSE if immediate conflict.
        private bool assume(Lit p)
        {
            this.trail_lim.Push(this.trail.Size());
            return this.enqueue(p);
        }


        // Revert to the state at given level.
        protected void cancelUntil(int level)
        {
            this.CancelUntilCallback(level);
            if (this.decisionLevel() > level)
            {
                for (int c = this.trail.Size() - 1; c >= this.trail_lim[level]; c--)
                {
                    Var x = var(this.trail[c]);
                    this.assigns[x] = LiftedBool.Value.Undef0;
                    this.reason[x] = null;
                    this.order.undo(x);
                }
                this.trail.Shrink(this.trail.Size() - this.trail_lim[level]);
                this.trail_lim.Shrink(this.trail_lim.Size() - level);
                this.qhead = this.trail.Size();
            }
        }
        #endregion

        #region Major methods:
        /*_________________________________________________________________________________________________
        |
        |  analyze : (confl : Clause*) (out_learnt : vec<Lit>&) (out_btlevel : int&)  .  [void]
        |  
        |  Description:
        |    Analyze conflict and produce a reason clause.
        |  
        |    Pre-conditions:
        |      * 'out_learnt' is assumed to be cleared.
        |      * Current decision level must be greater than root level.
        |  
        |    Post-conditions:
        |      * 'out_learnt[0]' is the asserting literal at level 'out_btlevel'.
        |  
        |  Effect:
        |    Will undo part of the trail, upto but not beyond the assumption of the current decision level.
        |________________________________________________________________________________________________@*/
        private void analyze(Clause confl, Vec<Lit> out_learnt, out int out_btlevel)
        {
            Vec<LiftedBool.Value> seen = this.analyze_seen;
            int pathC = 0;
            Lit p = lit_Undef;

            this.AdditionalConflictAnalisis(confl.GetData(), confl);

            // Generate conflict clause:
            //
            out_learnt.Push(new Lit());      // (leave room for the asserting literal)
            out_btlevel = 0;
            int index = this.trail.Size() - 1;
            //debug("start analyze");
            do
            {
                /*
                    debug("    loop analyze {0} {1} {2}\n", confl, p, p==lit_Undef ? -1 : level[var(p)]);
                    if (confl == null)
                    {
                      for (int i = trail.size()-1; i >= 0; i--)
                        debug("   {0} {1} {2} {3}\n", trail[i], seen[var(trail[i])], 
                                level[var(trail[i])], reason[var(trail[i])]);
                    } */
                Utils.Assert(confl != null);          // (otherwise should be UIP)

                Clause c = confl;

                if (c.learnt())
                {
                    this.claBumpActivity(c);
                }


                for (int j = (p == lit_Undef) ? 0 : 1; j < c.size(); j++)
                {
                    Lit q = c[j];
                    if (seen[var(q)] == 0 && this.level[var(q)] > 0)
                    {
                        this.varBumpActivity(q);
                        seen[var(q)] = LiftedBool.Value.True;
                        if (this.level[var(q)] == this.decisionLevel())
                        {
                            pathC++;
                        }

                        else
                        {
                            out_learnt.Push(q);
                            out_btlevel = Math.Max(out_btlevel, this.level[var(q)]);
                        }
                    }
                }

                // Select next clause to look at:
                while (seen[var(this.trail[index--])] == 0)
                {
                    ;
                }


                p = this.trail[index + 1];
                confl = this.reason[var(p)];
                seen[var(p)] = 0;
                pathC--;

            } while (pathC > 0);
            out_learnt[0] = ~p;

            // Conflict clause minimization:
            {
                int i, j;
                if (this.expensive_ccmin)
                {
                    // Simplify conflict clause (a lot):
                    //
                    uint min_level = 0;
                    for (i = 1; i < out_learnt.Size(); i++)
                    {
                        min_level |= (uint)(1 << (this.level[var(out_learnt[i])] & 31));         // (maintain an abstraction of levels involved in conflict)
                    }

                    this.analyze_toclear.Clear();
                    for (i = j = 1; i < out_learnt.Size(); i++)
                    {
                        if (this.reason[var(out_learnt[i])] == null || !this.analyze_removable(out_learnt[i], min_level))
                        {
                            out_learnt[j++] = out_learnt[i];
                        }
                    }

                }
                else
                {
                    // Simplify conflict clause (a little):
                    //
                    this.analyze_toclear.Clear();
                    for (i = j = 1; i < out_learnt.Size(); i++)
                    {
                        Clause r = this.reason[var(out_learnt[i])];
                        if (r == null)
                        {
                            out_learnt[j++] = out_learnt[i];
                        }

                        else
                        {
                            Clause c = r;
                            for (int k = 1; k < c.size(); k++)
                            {

                                if (seen[var(c[k])] == 0 && this.level[var(c[k])] != 0)
                                {
                                    out_learnt[j++] = out_learnt[i];
                                    goto Keep;
                                }
                            }


                            this.analyze_toclear.Push(out_learnt[i]);
                        Keep:;
                        }
                    }
                }


                // Clean up:
                //
                {
                    int jj;
                    for (jj = 0; jj < out_learnt.Size(); jj++)
                    {
                        seen[var(out_learnt[jj])] = 0;
                    }


                    for (jj = 0; jj < this.analyze_toclear.Size(); jj++)
                    {
                        seen[var(this.analyze_toclear[jj])] = 0;    // ('seen[]' is now cleared)
                    }

                }


                this.stats.max_literals += out_learnt.Size();
                out_learnt.Shrink(i - j);
                this.stats.tot_literals += out_learnt.Size();

            }
        }


        // Check if 'p' can be removed. 'min_level' is used to abort early if visiting literals at a level that cannot be removed.
        //
        private bool analyze_removable(Lit p_, uint min_level)
        {
            Utils.Assert(this.reason[var(p_)] != null);
            this.analyze_stack.Clear(); this.analyze_stack.Push(p_);
            int top = this.analyze_toclear.Size();
            while (this.analyze_stack.Size() > 0)
            {
                Utils.Assert(this.reason[var(this.analyze_stack.Last())] != null);
                Clause c = this.reason[var(this.analyze_stack.Last())];
                this.analyze_stack.Pop();
                for (int i = 1; i < c.size(); i++)
                {
                    Lit p = c[i];
                    if (this.analyze_seen[var(p)] == 0 && this.level[var(p)] != 0)
                    {
                        if (this.reason[var(p)] != null && ((1 << (this.level[var(p)] & 31)) & min_level) != 0)
                        {
                            this.analyze_seen[var(p)] = LiftedBool.Value.True;
                            this.analyze_stack.Push(p);
                            this.analyze_toclear.Push(p);
                        }
                        else
                        {
                            for (int j = top; j < this.analyze_toclear.Size(); j++)
                            {
                                this.analyze_seen[var(this.analyze_toclear[j])] = 0;
                            }


                            this.analyze_toclear.Shrink(this.analyze_toclear.Size() - top);
                            return false;
                        }
                    }
                }
            }

            this.analyze_toclear.Push(p_);

            return true;
        }


        /*_________________________________________________________________________________________________
        |
        |  analyzeFinal : (confl : Clause*) (skip_first : bool)  .  [void]
        |  
        |  Description:
        |    Specialized analysis procedure to express the final conflict in terms of assumptions.
        |    'root_level' is allowed to point beyond end of trace (useful if called after conflict while
        |    making assumptions). If 'skip_first' is TRUE, the first literal of 'confl' is  ignored (needed
        |    if conflict arose before search even started).
        |________________________________________________________________________________________________@*/
        private void analyzeFinal(Clause confl, bool skip_first)
        {
            // -- NOTE! This code is relatively untested. Please report bugs!
            this.conflict.Clear();
            if (this.root_level == 0)
            {
                return;
            }


            Vec<LiftedBool.Value> seen = this.analyze_seen;
            for (int i = skip_first ? 1 : 0; i < confl.size(); i++)
            {
                Var x = var(confl[i]);
                if (this.level[x] > 0)
                {
                    seen[x] = LiftedBool.Value.True;
                }
            }

            int start = (this.root_level >= this.trail_lim.Size()) ? this.trail.Size() - 1 : this.trail_lim[this.root_level];
            for (int i = start; i >= this.trail_lim[0]; i--)
            {
                Var x = var(this.trail[i]);
                if (seen[x] != 0)
                {
                    Clause r = this.reason[x];
                    if (r == null)
                    {
                        Utils.Assert(this.level[x] > 0);
                        this.conflict.Push(~this.trail[i]);
                    }
                    else
                    {
                        Clause c = r;
                        for (int j = 1; j < c.size(); j++)
                        {
                            if (this.level[var(c[j])] > 0)
                            {
                                seen[var(c[j])] = LiftedBool.Value.True;
                            }
                        }

                    }
                    seen[x] = LiftedBool.Value.Undef0;
                }
            }
        }


        /*_________________________________________________________________________________________________
        |
        |  enqueue : (p : Lit) (from : Clause*)  .  [bool]
        |  
        |  Description:
        |    Puts a new fact on the propagation queue as well as immediately updating the variable's value.
        |    Should a conflict arise, FALSE is returned.
        |  
        |  Input:
        |    p    - The fact to enqueue
        |    from - [Optional] Fact propagated from this (currently) unit clause. Stored in 'reason[]'.
        |           Default value is null (no reason).
        |  
        |  Output:
        |    TRUE if fact was enqueued without conflict, FALSE otherwise.
        |________________________________________________________________________________________________@*/
        private bool enqueue(Lit p, Clause from)
        {
            if (!LiftedBool.IsUndef(this.value(p)))
            {
                return this.value(p) != LiftedBool.Value.False;
            }
            else
            {
                Var x = var(p);
                this.assigns[x] = LiftedBool.From(!sign(p));
                this.level[x] = this.decisionLevel();
                this.trail_pos[x] = this.trail.Size();
                this.reason[x] = from;
                this.trail.Push(p);
                return true;
            }
        }


        /*_________________________________________________________________________________________________
        |
        |  propagate : [void]  .  [Clause*]
        |  
        |  Description:
        |    Propagates all enqueued facts. If a conflict arises, the conflicting clause is returned,
        |    otherwise null. NOTE! This method has been optimized for speed rather than readability.
        |  
        |    Post-conditions:
        |      * the propagation queue is empty, even if there was a conflict.
        |________________________________________________________________________________________________@*/
        private Clause propagate()
        {
            Clause confl = null;
            while (this.qhead < this.trail.Size())
            {
                this.stats.propagations++;
                this.simpDB_props--;

                Lit p = this.trail[this.qhead++];     // 'p' is enqueued fact to propagate.
                Vec<Clause> ws = this.watches[index(p)];
                //GClause*       i,* j, *end;
                int i, j, end;

                for (i = j = 0, end = i + ws.Size(); i != end;)
                {
                    Clause c = ws[i++];
                    // Make sure the false literal is data[1]:
                    Lit false_lit = ~p;
                    if (c[0] == false_lit)
                    { c[0] = c[1]; c[1] = false_lit; }

                    Utils.Assert(c[1] == false_lit);

                    // If 0th watch is true, then clause is already satisfied.
                    Lit first = c[0];
                    LiftedBool.Value val = this.value(first);
                    if (val == LiftedBool.Value.True)
                    {
                        ws[j++] = c;
                    }
                    else
                    {
                        // Look for new watch:
                        for (int k = 2; k < c.size(); k++)
                        {

                            if (this.value(c[k]) != LiftedBool.Value.False)
                            {
                                c[1] = c[k]; c[k] = false_lit;
                                this.watches[index(~c[1])].Push(c);
                                goto FoundWatch;
                            }
                        }

                        // Did not find watch -- clause is unit under assignment:

                        ws[j++] = c;
                        if (!this.enqueue(first, c))
                        {
                            if (this.decisionLevel() == 0)
                            {
                                this.ok = false;
                            }


                            confl = c;
                            this.qhead = this.trail.Size();
                            // Copy the remaining watches:
                            while (i < end)
                            {
                                ws[j++] = ws[i++];
                            }

                        }
                    FoundWatch:;
                    }
                }
                ws.Shrink(i - j);
            }

            return confl;
        }


        /*_________________________________________________________________________________________________
        |
        |  reduceDB : ()  .  [void]
        |  
        |  Description:
        |    Remove half of the learnt clauses, minus the clauses locked by the current assignment. Locked
        |    clauses are clauses that are reason to some assignment. Binary clauses are never removed.
        |________________________________________________________________________________________________@*/
        private class reduceDB_lt : IComparer<Clause>
        {
            public int Compare(Clause x, Clause y)
            {
                if (x.size() > 2 && (y.size() == 2 || x.activity < y.activity))
                {

                    return -1;
                }
                else
                {
                    return 1;
                }

            }
        }

        private void reduceDB()
        {
            int i, j;
            double extra_lim = this.cla_inc / this.learnts.Size();    // Remove any clause below this activity

            this.learnts.Sort(new reduceDB_lt());
            for (i = j = 0; i < this.learnts.Size() / 2; i++)
            {
                if (this.learnts[i].size() > 2 && !this.locked(this.learnts[i]))
                {
                    this.remove(this.learnts[i]);
                }
                else
                {
                    this.learnts[j++] = this.learnts[i];
                }

            }
            for (; i < this.learnts.Size(); i++)
            {
                if (this.learnts[i].size() > 2 && !this.locked(this.learnts[i]) && this.learnts[i].activity < extra_lim)
                {
                    this.remove(this.learnts[i]);
                }
                else
                {
                    this.learnts[j++] = this.learnts[i];
                }

            }
            this.learnts.Shrink(i - j);
        }


        /*_________________________________________________________________________________________________
        |
        |  simplifyDB : [void]  .  [bool]
        |  
        |  Description:
        |    Simplify the clause database according to the current top-level assigment. Currently, the only
        |    thing done here is the removal of satisfied clauses, but more things can be put here.
        |________________________________________________________________________________________________@*/
        private void simplifyDB()
        {
            if (!this.ok)
            {
                return;    // GUARD (public method)
            }


            Utils.Assert(this.decisionLevel() == 0);

            if (this.propagate() != null)
            {
                this.ok = false;
                return;
            }

            if (this.nAssigns() == this.simpDB_assigns || this.simpDB_props > 0)   // (nothing has changed or preformed a simplification too recently)
            {
                return;
            }

            // Clear watcher lists:

            for (int i = this.simpDB_assigns; i < this.nAssigns(); i++)
            {
                Lit p = this.trail[i];
                this.watches[index(p)].Clear();
                this.watches[index(~p)].Clear();
            }

            // Remove satisfied clauses:
            for (int type = 0; type < 2; type++)
            {
                Vec<Clause> cs = type != 0 ? this.learnts : this.clauses;
                int j = 0;
                for (int i = 0; i < cs.Size(); i++)
                {
                    if (!this.locked(cs[i]) && this.simplify(cs[i]))
                    {
                        this.remove(cs[i]);
                        this.RemoveClauseCallback(cs[i]);
                    }
                    else
                    {
                        cs[j++] = cs[i];
                    }

                }
                cs.Shrink(cs.Size() - j);
            }

            this.simpDB_assigns = this.nAssigns();
            this.simpDB_props = this.stats.clauses_literals + this.stats.learnts_literals;   // (shouldn't depend on 'stats' really, but it will do for now)
        }


        /*_________________________________________________________________________________________________
        |
        |  search : (nof_conflicts : int) (nof_learnts : int) (parms : const SearchParams&)  .  [lbool]
        |  
        |  Description:
        |    Search for a model the specified number of conflicts, keeping the number of learnt clauses
        |    below the provided limit. NOTE! Use negative value for 'nof_conflicts' or 'nof_learnts' to
        |    indicate infinity.
        |  
        |  Output:
        |    'LiftedBool.Value.True' if a partial assigment that is consistent with respect to the clauseset is found. If
        |    all variables are decision variables, this means that the clause set is satisfiable. 'LiftedBool.Value.False'
        |    if the clause set is unsatisfiable. 'l_Undef' if the bound on number of conflicts is reached.
        |________________________________________________________________________________________________@*/
        private LiftedBool.Value search(int nof_conflicts, int nof_learnts, SearchParams parms)
        {
            if (!this.ok)
            {
                return LiftedBool.Value.False;    // GUARD (public method)
            }


            Utils.Assert(this.root_level == this.decisionLevel());

            this.stats.starts++;
            int conflictC = 0;
            this.var_decay = 1 / parms.var_decay;
            this.cla_decay = 1 / parms.clause_decay;
            this.model.Clear();

            for (; ; )
            {
                Clause confl = this.propagate();
                if (confl != null)
                {
                    // CONFLICT

                    this.stats.conflicts++; conflictC++;
                    Vec<Lit> learnt_clause = new();
                    int backtrack_level;
                    if (this.decisionLevel() == this.root_level)
                    {
                        // Contradiction found:
                        this.analyzeFinal(confl);
                        return LiftedBool.Value.False;
                    }
                    this.analyze(confl, learnt_clause, out backtrack_level);
                    this.cancelUntil(Math.Max(backtrack_level, this.root_level));
                    this.newClause(learnt_clause, true);
                    if (learnt_clause.Size() == 1)
                    {
                        this.level[var(learnt_clause[0])] = 0;    // (this is ugly (but needed for 'analyzeFinal()') -- in future versions, we will backtrack past the 'root_level' and redo the assumptions)
                    }


                    this.varDecayActivity();
                    this.claDecayActivity();

                }
                else
                {
                    // NO CONFLICT

                    if (nof_conflicts >= 0 && conflictC >= nof_conflicts)
                    {
                        // Reached bound on number of conflicts:
                        this.progress_estimate = this.progressEstimate();
                        this.cancelUntil(this.root_level);
                        return LiftedBool.Value.Undef0;
                    }

                    if (this.decisionLevel() == 0)
                    // Simplify the set of problem clauses:
                    {
                        this.simplifyDB(); if (!this.ok)
                        {
                            return LiftedBool.Value.False;
                        }
                    }

                    if (nof_learnts >= 0 && this.learnts.Size() - this.nAssigns() >= nof_learnts)
                    {
                        // Reduce the set of learnt clauses:
                        this.reduceDB();
                    }

                    // New variable decision:

                    this.stats.decisions++;
                    Lit next = this.order.select(parms.random_var_freq);

                    if (next == lit_Undef)
                    {
                        if (this.ModelFound())
                        {
                            continue;
                        }
                        // Model found:

                        this.model.GrowTo(this.nVars());
                        for (int i = 0; i < this.nVars(); i++)
                        {
                            this.model[i] = this.value(i);
                        }


                        this.cancelUntil(this.root_level);
                        return LiftedBool.Value.True;
                    }

                    Utils.Check(this.assume(next));
                }
            }
        }


        // Divide all variable activities by 1e100.
        //
        private void varRescaleActivity()
        {
            for (int i = 0; i < this.nVars(); i++)
            {
                this.activity[i] *= 1e-100;
            }


            this.var_inc *= 1e-100;
        }


        // Divide all constraint activities by 1e20.
        //
        private void claRescaleActivity()
        {
            for (int i = 0; i < this.learnts.Size(); i++)
            {
                this.learnts[i].activity *= 1e-20f;
            }


            this.cla_inc *= 1e-20;
        }


        /*_________________________________________________________________________________________________
        |
        |  solve : (assumps : const vec<Lit>&)  .  [bool]
        |  
        |  Description:
        |    Top-level solve. If using assumptions (non-empty 'assumps' vector), you must call
        |    'simplifyDB()' first to see that no top-level conflict is present (which would put the solver
        |    in an undefined state).
        |
        |  Input:
        |    A list of assumptions (unit clauses coded as literals). Pre-condition: The assumptions must
        |    not contain both 'x' and '~x' for any variable 'x'.
        |________________________________________________________________________________________________@*/
        private bool solve(Vec<Lit> assumps)
        {
            this.simplifyDB();
            if (!this.ok)
            {
                return false;
            }


            SearchParams parms = new(this.default_parms);
            double nof_conflicts = 100;
            double nof_learnts = this.nClauses() / 3;
            LiftedBool.Value status = LiftedBool.Value.Undef0;

            // Perform assumptions:
            this.root_level = assumps.Size();
            for (int i = 0; i < assumps.Size(); i++)
            {
                Lit p = assumps[i];
                Utils.Assert(var(p) < this.nVars());
                if (!this.assume(p))
                {
                    Clause r = this.reason[var(p)];
                    if (r != null)
                    {
                        this.analyzeFinal(r, true);
                        this.conflict.Push(~p);
                    }
                    else
                    {
                        this.conflict.Clear();
                        this.conflict.Push(~p);
                    }
                    this.cancelUntil(0);
                    return false;
                }

                {
                    Clause confl = this.propagate();
                    if (confl != null)
                    {
                        this.analyzeFinal(confl);
                        Utils.Assert(this.conflict.Size() > 0);
                        this.cancelUntil(0);
                        return false;
                    }
                }
            }
            Utils.Assert(this.root_level == this.decisionLevel());

            // Search:
            if (this.verbosity >= 1)
            {
                reportf("==================================[MINISAT]===================================\n");
                reportf("| Conflicts |     ORIGINAL     |              LEARNT              | Progress |\n");
                reportf("|           | Clauses Literals |   Limit Clauses Literals  Lit/Cl |          |\n");
                reportf("==============================================================================\n");
            }

            while (LiftedBool.IsUndef(status))
            {
                if (this.verbosity >= 1)
                {
                    reportf("| {0,9} | {1,7} {2,8} | {3,7} {4,7} {5,8} {6,7:0.0} |{7,6:0.000} %% |\n",
                        (int)this.stats.conflicts, this.nClauses(), (int)this.stats.clauses_literals,
                        (int)nof_learnts, this.nLearnts(), (int)this.stats.learnts_literals,
                        (double)this.stats.learnts_literals / this.nLearnts(), this.progress_estimate * 100);
                }


                status = this.search((int)nof_conflicts, (int)nof_learnts, parms);
                nof_conflicts *= 1.5;
                nof_learnts *= 1.1;
            }
            if (this.verbosity >= 1)
            {
                reportf("==============================================================================\n");
            }


            this.cancelUntil(0);
            return status == LiftedBool.Value.True;
        }
        #endregion

        #region Stats
        private double start_time = cpuTime();

        // Return search-space coverage. Not extremely reliable.
        //
        private double progressEstimate()
        {
            double progress = 0;
            double F = 1.0 / this.nVars();
            for (int i = 0; i < this.nVars(); i++)
            {

                if (!LiftedBool.IsUndef(this.value(i)))
                {
                    progress += Math.Pow(F, this.level[i]);
                }
            }


            return progress / this.nVars();
        }


        public void printStats()
        {
            double cpu_time = cpuTime() - this.start_time;
            long mem_used = memUsed();
            reportf("restarts              : {0,12}\n", this.stats.starts);
            reportf("conflicts             : {0,12}   ({1:0.0} /sec)\n", this.stats.conflicts, this.stats.conflicts / cpu_time);
            reportf("decisions             : {0,12}   ({1:0.0} /sec)\n", this.stats.decisions, this.stats.decisions / cpu_time);
            reportf("propagations          : {0,12}   ({1:0.0} /sec)\n", this.stats.propagations, this.stats.propagations / cpu_time);
            reportf("conflict literals     : {0,12}   ({1:0.00} %% deleted)\n", this.stats.tot_literals, (this.stats.max_literals - this.stats.tot_literals) * 100 / (double)this.stats.max_literals);
            if (mem_used != 0)
            {
                reportf("Memory used           : {0:0.00} MB\n", mem_used / 1048576.0);
            }


            reportf("CPU time              : {0:0.000} s\n", cpu_time);
        }
        #endregion


        #region DPLL(T) stuff -- don't use yet
        // cancelUntil was called with level
        protected virtual void CancelUntilCallback(int level)
        {
        }

        private int levelToBacktrack;

        private bool ModelFound()
        {
            this.levelToBacktrack = int.MaxValue;

            bool res = this.ModelFoundCallback();

            if (this.levelToBacktrack != int.MaxValue)
            {
                this.cancelUntil(this.levelToBacktrack);
                this.qhead = this.trail_lim.Size() == 0 ? 0 : this.trail_lim.Last();
            }

            if (!this.ok)
            {

                return false;
            }


            return res;
        }

        private void MoveBack(Lit l1, Lit l2)
        {

            int lev1 = this.level[var(l1)];
            int lev2 = this.level[var(l2)];
            if (lev1 == -1)
            {
                lev1 = int.MaxValue;
            }

            if (lev2 == -1)
            {
                lev2 = int.MaxValue;
            }


            if (lev1 < this.levelToBacktrack || lev2 < this.levelToBacktrack)
            {
                if (this.value(l1) == LiftedBool.Value.True)
                {
                    if (this.value(l2) == LiftedBool.Value.True)
                    { }
                    else if (lev1 <= lev2 || this.levelToBacktrack <= lev2)
                    { }
                    else
                    {
                        this.levelToBacktrack = lev2;
                    }

                }
                else
                {
                    if (this.value(l2) == LiftedBool.Value.True)
                    {
                        if (lev2 <= lev1 || this.levelToBacktrack <= lev1)
                        { }
                        else
                        {
                            this.levelToBacktrack = lev1;
                        }
                    }
                    else
                    {
                        this.levelToBacktrack = Math.Min(lev1, lev2);
                    }

                }

            }

            //debug("level: {0} {1}", levelToBacktrack, l);
        }

        // this is expected to return true if it adds some new conflict clauses
        protected virtual bool ModelFoundCallback()
        {
            return false;
        }

        public bool SearchNoRestarts()
        {
            SearchParams parms = new(this.default_parms);
            LiftedBool.Value status = LiftedBool.Value.Undef0;

            this.simplifyDB();
            if (!this.ok)
            {
                return false;
            }


            this.root_level = 0;
            Utils.Assert(this.root_level == this.decisionLevel());

            while (LiftedBool.IsUndef(status))
            {
                status = this.search(-1, -1, parms);
            }

            this.cancelUntil(0);
            return this.ok && status == LiftedBool.Value.True;
        }

        protected virtual void NewClauseCallback(Clause c)
        {
        }

        protected virtual void RemoveClauseCallback(Clause c)
        {
        }

        protected virtual void AdditionalConflictAnalisis(Lit[] conflict, Clause c)
        {
        }
        #endregion

    } // end class Solver

} // end namespace
