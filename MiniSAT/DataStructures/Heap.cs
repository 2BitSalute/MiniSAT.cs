namespace MiniSAT.DataStructures
{
    using System;

    using MiniSAT.Utils;

    public class Heap
    {
        private readonly Func<int, int, bool> lessThan;

        /// <summary>
        /// A heap of ints is an array
        /// </summary>
        public Vec<int> heap = new();

        /// <summary>
        /// int . index in the heap
        /// </summary>
        public Vec<int> indices = new();

        private static int Left(int i) { return i + i; }

        private static int Right(int i) => i + i + 1;

        private static int Parent(int i) => i >> 1;

        public Heap(Func<int, int, bool> lessThan)
        {
            this.lessThan = lessThan;
            this.heap.Push(-1);
        }

        public void PercolateUp(int i)
        {
            int x = this.heap[i];

            while (Parent(i) != 0 && this.lessThan(x, this.heap[Parent(i)]))
            {
                this.heap[i] = this.heap[Parent(i)];
                this.indices[this.heap[i]] = i;
                i = Parent(i);
            }

            this.heap[i] = x;
            this.indices[x] = i;
        }

        public void PercolateDown(int index)
        {
            int value = this.heap[index];

            while (Left(index) < this.heap.Size())
            {
                int child = Right(index) < this.heap.Size() && this.lessThan(this.heap[Right(index)], this.heap[Left(index)]) ? Right(index) : Left(index);
                if (!this.lessThan(this.heap[child], value))
                {
                    break;
                }

                this.heap[index] = this.heap[child];
                this.indices[this.heap[index]] = index;
                index = child;
            }

            this.heap[index] = value;
            this.indices[value] = index;
        }

        public bool Ok(int n) => n >= 0 && n < this.indices.Size();

        public void SetBounds(int size)
        {
            Common.Assert(size >= 0);

            this.indices.GrowTo(size, 0);
        }

        public bool InHeap(int n)
        {
            Common.Assert(this.Ok(n));

            return this.indices[n] != 0;
        }

        public void Increase(int n)
        {
            Common.Assert(this.Ok(n));
            Common.Assert(this.InHeap(n));
            this.PercolateUp(this.indices[n]);
        }

        public bool Empty() => this.heap.Size() == 1;

        public void Insert(int n)
        {
            Common.Assert(this.Ok(n));
            this.indices[n] = this.heap.Size();
            this.heap.Push(n);
            this.PercolateUp(this.indices[n]);
        }

        public int GetMin()
        {
            int root = this.heap[1];
            this.heap[1] = this.heap.Last();
            this.indices[this.heap[1]] = 1;
            this.indices[root] = 0;
            this.heap.Pop();

            if (this.heap.Size() > 1)
            {
                this.PercolateDown(1);
            }

            return root;
        }

        public bool HeapProperty()
        {
            return this.HeapProperty(index: 1);
        }

        public bool HeapProperty(int index)
        {
            return
                index >= this.heap.Size()
                || (Parent(index) == 0 || !this.lessThan(this.heap[index], this.heap[Parent(index)]))
                && this.HeapProperty(Left(index))
                && this.HeapProperty(Right(index));
        }
    }


} // end namespace MiniSat
