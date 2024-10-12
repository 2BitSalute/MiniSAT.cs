namespace MiniSAT
{
    // NOTE! Variables are just integers. No abstraction here. They should be chosen from 0..N,
    // so that they can be used as array indices.
    using Var = System.Int32;

    using MiniSAT.DataStructures;
    using MiniSAT.Utils;

    public class VarOrder
    {
        readonly protected Vec<LiftedBool.Value> assigns;     // var.val. Pointer to external assignment table.
        readonly protected Vec<double> activity;    // var.act. Pointer to external activity table.
        protected Heap heap;
        private double random_seed; // For the internal random number generator

        public VarOrder(Vec<LiftedBool.Value> ass, Vec<double> act)
        {
            this.assigns = ass;
            this.activity = act;
            this.heap = new Heap(this.LessThan);
            this.random_seed = 91648253;
        }

        private bool LessThan(Var x, Var y) { return this.activity[x] > this.activity[y]; }

        public virtual void newVar()
        {
            this.heap.SetBounds(this.assigns.Size());
            this.heap.Insert(this.assigns.Size() - 1);
        }

        // Called when variable increased in activity.
        public virtual void update(Var x)
        {
            if (this.heap.InHeap(x))
            {
                this.heap.Increase(x);
            }

        }

        // Called when variable is unassigned and may be selected again.
        public virtual void undo(Var x)
        {
            if (!this.heap.InHeap(x))
            {
                this.heap.Insert(x);
            }

        }

        public Lit select()
        {
            return this.select(0.0);
        }

        // Selects a new, unassigned variable (or 'var_Undef' if none exists).
        public virtual Lit select(double random_var_freq)
        {
            // Random decision:
            if (Common.drand(ref this.random_seed) < random_var_freq && !this.heap.Empty())
            {
                Var next = Common.irand(ref this.random_seed, this.assigns.Size());
                if (LiftedBool.IsUndef(this.assigns[next]))
                {

                    return ~new Lit(next);
                }

            }

            // Activity based decision:
            while (!this.heap.Empty())
            {
                Var next = this.heap.GetMin();
                if (LiftedBool.IsUndef(this.assigns[next]))
                {

                    return ~new Lit(next);
                }

            }

            return Lit.lit_Undef;
        }
    }
}