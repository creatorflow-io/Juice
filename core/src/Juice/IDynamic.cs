using System.Runtime.CompilerServices;

namespace Juice
{
    public interface IDynamic
    {
        T? GetProperty<T>(Func<T>? defaultValue = null, [CallerMemberName] string? name = null);
        void SetProperty<T>(T? value, [CallerMemberName] string? name = null);
        object? this[string key] { get; }
    }
}
