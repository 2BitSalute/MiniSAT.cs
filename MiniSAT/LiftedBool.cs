namespace MiniSatCS
{
    public static class LiftedBool
    {
        /// <summary>
        /// Possible lifted boolean values
        /// </summary>
        /// <remarks>
        /// The problem is that C# allows the use of ~ on any enum type
        /// therefore, ~lbool.True == lbool.False, but we also end up using 
        /// two undef values
        /// </remarks>
        public enum Value : sbyte
        {
            True = 1,
            False = -2,
            Undef0 = 0,
            Undef1 = -1
        }

        public const Value True = Value.True;
        public const Value False = Value.False;

        public static Value From(bool v) { return v ? Value.True : Value.False; }

        public static bool IsUndef(Value l)
        {
            return l != Value.True && l != Value.False;
        }
    }
}