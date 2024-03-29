﻿using System.ComponentModel.DataAnnotations.Schema;

namespace Juice.Domain
{
    public abstract class ValueObject : IValidatable
    {
        [NotMapped]
        public IList<string> ValidationErrors => new List<string>();

        protected static bool EqualOperator(ValueObject left, ValueObject right)
        {
            if (ReferenceEquals(left, null) ^ ReferenceEquals(right, null))
            {
                return false;
            }
            return ReferenceEquals(left, null) || left.Equals(right);
        }
        protected static bool NotEqualOperator(ValueObject left, ValueObject right)
        {
            return !(EqualOperator(left, right));
        }
        protected abstract IEnumerable<object> GetEqualityComponents();
        public override bool Equals(object? obj)
        {
            if (obj == null || obj.GetType() != GetType())
            {
                return false;
            }
            var other = (ValueObject)obj;
            return this.GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
        }
        public override int GetHashCode()
        {
            return GetEqualityComponents()
            .Select(x => x != null ? x.GetHashCode() : 0).Aggregate((x, y) => x ^ y);
        }
        // Other utility methods

        public static bool operator ==(ValueObject one, ValueObject two)
        {
            return one?.Equals(two) ?? false;
        }
        public static bool operator !=(ValueObject one, ValueObject two)
        {
            return !(one?.Equals(two) ?? false);
        }
    }
}
