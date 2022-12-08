using System.Runtime.CompilerServices;

namespace Juice.Domain
{
    public interface IDynamic : IExpandable
    {
        T GetProperty<T>(Func<T>? defaultValue = null, [CallerMemberName] string? name = null);
        void SetProperty(object value, [CallerMemberName] string? name = null);
    }
}
