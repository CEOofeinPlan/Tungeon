using System;
using System.Collections.Generic;

namespace Hilfe
{
    public struct Pair<T1, T2>
    {
        public T1 first;
        public T2 second;
        public Pair(T1 f, T2 s)
        {
            first = f;
            second = s;
        }

        public override bool Equals(object obj)
        {
            if (obj is Pair<T1, T2> other)
            {
                return EqualityComparer<T1>.Default.Equals(first, other.first) &&
                       EqualityComparer<T2>.Default.Equals(second, other.second);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return new { first, second }.GetHashCode();
        }
    };
}