using Microsoft.AspNetCore.Authorization;

namespace Juice.Storage.Authorization
{
    public class StorageAuthorizationRequirement : IAuthorizationRequirement
    {
        public StorageAuthorizationRequirement(string name)
        {
            Name = name;
        }
        public string Name { get; init; }
    }
}
