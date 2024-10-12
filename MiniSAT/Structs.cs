/******************************************************************************************
MiniSat -- Copyright (c) 2003-2005, Niklas Een, Niklas Sorensson
MiniSatCS -- Copyright (c) 2006, Michal Moskal

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

using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;

namespace MiniSatCS
{
    public delegate bool IntLess(int i1, int i2);

    public class Heap
    {
        public IntLess comp;

        /// <summary>
        /// A heap of ints
        /// </summary>
        public Vec<int> heap = new();

        /// <summary>
        /// int . index in the heap
        /// </summary>
        public Vec<int> indices = new();

        private static int Left(int i) { return i + i; }

        private static int Right(int i) => i + i + 1;

        private static int Parent(int i) => i >> 1;

        public void PercolateUp(int i)
        {
            int x = this.heap[i];
            while (Parent(i) != 0 && this.comp(x, this.heap[Parent(i)]))
            {
                this.heap[i] = this.heap[Parent(i)];
                this.indices[this.heap[i]] = i;
                i = Parent(i);
            }
            this.heap[i] = x;
            this.indices[x] = i;
        }

        public void PercolateDown(int i)
        {
            int x = this.heap[i];
            while (Left(i) < this.heap.Size())
            {
                int child = Right(i) < this.heap.Size() && this.comp(this.heap[Right(i)], this.heap[Left(i)]) ? Right(i) : Left(i);
                if (!this.comp(this.heap[child], x))
                {
                    break;
                }


                this.heap[i] = this.heap[child];
                this.indices[this.heap[i]] = i;
                i = child;
            }
            this.heap[i] = x;
            this.indices[x] = i;
        }

        public bool ok(int n) { return n >= 0 && n < (int)this.indices.Size(); }

        public Heap(IntLess c) { this.comp = c; this.heap.Push(-1); }

        public void setBounds(int size) { Solver.assert(size >= 0); this.indices.GrowTo(size, 0); }
        public bool inHeap(int n) { Solver.assert(this.ok(n)); return this.indices[n] != 0; }
        public void increase(int n) { Solver.assert(this.ok(n)); Solver.assert(this.inHeap(n)); this.PercolateUp(this.indices[n]); }
        public bool empty() { return this.heap.Size() == 1; }

        public void insert(int n)
        {
            //reportf("{0} {1}\n", n, indices.size());
            Solver.assert(this.ok(n));
            this.indices[n] = this.heap.Size();
            this.heap.Push(n);
            this.PercolateUp(this.indices[n]);
        }

        public int getmin()
        {
            int r = this.heap[1];
            this.heap[1] = this.heap.Last();
            this.indices[this.heap[1]] = 1;
            this.indices[r] = 0;
            this.heap.Pop();
            if (this.heap.Size() > 1)
            {
                this.PercolateDown(1);
            }


            return r;
        }

        public bool heapProperty()
        {
            return this.heapProperty(1);
        }

        public bool heapProperty(int i)
        {
            return i >= this.heap.Size()
                || ((Parent(i) == 0 || !this.comp(this.heap[i], this.heap[Parent(i)])) && this.heapProperty(Left(i)) && this.heapProperty(Right(i)));
        }
    }


} // end namespace MiniSat
