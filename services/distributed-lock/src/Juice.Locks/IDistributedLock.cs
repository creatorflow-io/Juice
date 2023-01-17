using System.Linq.Expressions;

namespace Juice.Locks
{
    public interface IDistributedLock
    {
        ILock? AcquireLock(string key, string issuer, TimeSpan expiration);
        Task<ILock?> AcquireLockAsync(string key, string issuer, TimeSpan expiration);
        bool ReleaseLock(ILock @lock);
        Task<bool> ReleaseLockAsync(ILock @lock);
    }

    public static class LockerExtensions
    {
        public static ILock? AcquireLock<T>(this IDistributedLock locker, T value, TimeSpan expiration,
            Expression<Func<T, object>> keySelector, string lockedBy)
        {
            var key = keySelector.Compile().Invoke(value).ToString() ?? "";
            key = (typeof(T).Name + ":" + key).Trim(':');
            return locker.AcquireLock(key, lockedBy, expiration);
        }

        public static Task<ILock?> AcquireLockAsync<T>(this IDistributedLock locker, T value, TimeSpan expiration,
            Expression<Func<T, object>> keySelector, string lockedBy)
        {
            var key = keySelector.Compile().Invoke(value).ToString() ?? "";
            key = (typeof(T).Name + ":" + key).Trim(':');
            return locker.AcquireLockAsync(key, lockedBy, expiration);
        }
    }
}
