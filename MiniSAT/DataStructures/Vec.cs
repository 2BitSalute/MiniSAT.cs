namespace MiniSatCS
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// 'vec' -- automatically resizable arrays (via 'push()' method)
    /// Can also be shrunk!
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Vec<T>
    {
        private T[] data;

        private int size;

        private void Grow(int min_cap)
        {
            int cap = this.data == null ? 0 : this.data.Length;
            if (min_cap <= cap)
            {
                return;
            }

            if (cap == 0)
            {
                cap = (min_cap >= 2) ? min_cap : 2;
            }
            else
            {
                do
                {
                    cap = (cap * 3 + 1) >> 1;
                }
                while (cap < min_cap);
            }

            Array.Resize(ref this.data, cap);
        }

        // Constructors:
        public Vec() { }

        public Vec(int size) { this.GrowTo(size); }

        public Vec(int size, T pad) { this.GrowTo(size, pad); }
        // (takes ownership of array)
        public Vec(T[] array)
        {
            this.data = array;
            this.size = this.data.Length;
        }

        // Size operations:
        public int Size() { return this.size; }

        public void Shrink(int nelems)
        {
            Solver.assert(nelems <= this.size);
            Solver.assert(nelems >= 0);
            this.size -= nelems;
        }
        public void ShrinkTo(int nelems)
        {
            this.Shrink(this.Size() - nelems);
        }

        public void Pop() { Solver.assert(this.size > 0); this.size--; }

        public void GrowTo(int size)
        {
            this.Grow(size);
            this.size = size;
        }

        public void GrowTo(int size, T pad)
        {
            int i = this.size;

            this.GrowTo(size);

            for (; i < this.size; ++i)
            {
                this.data[i] = pad;
            }
        }

        public void Clear()
        {
            this.size = 0;
        }

        public void Capacity(int size) { this.Grow(size); }

        // Stack interface:
        public void Push(T elem) { if (this.data == null || this.size == this.data.Length) { this.Grow(this.size + 1); } this.data[this.size++] = elem; }

        public T Last() { return this.data[this.size - 1]; }

        // Vector interface:
        public T this[int index]
        {
            get
            {
                Solver.assert(index < this.size);
                return this.data[index];
            }

            set
            {
                Solver.assert(index < this.size);
                this.data[index] = value;
            }
        }

        // Duplicatation (preferred instead):
        public void CopyTo(Vec<T> copy)
        {
            copy.Clear(); copy.GrowTo(this.size);
            if (this.size > 0)
            {
                Array.Copy(this.data, copy.data, this.size);
            }
        }

        public void MoveTo(Vec<T> dest)
        {
            dest.Clear(); dest.data = this.data; dest.size = this.size;
            this.data = null; this.size = 0;
        }

        public void Sort(IComparer<T> cmp)
        {
            if (this.size > 1)
            {
                Array.Sort(this.data, 0, this.size, cmp);
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < this.size; ++i)
            {
                yield return this.data[i];
            }
        }

        public void Swap(int i, int j)
        {
            Solver.assert(i < this.size && j < this.size);

            (this.data[j], this.data[i]) = (this.data[i], this.data[j]);
        }

        public T[] ToArray()
        {
            T[] res = new T[this.size];
            for (int i = 0; i < this.size; ++i)
            {
                res[i] = this.data[i];
            }

            return res;
        }
    }
}