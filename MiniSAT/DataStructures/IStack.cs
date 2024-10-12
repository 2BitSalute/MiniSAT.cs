namespace MiniSAT.DataStructures
{
    public interface IStack<T>
    {
        void Push(T elem);

        T Last();
    }
}