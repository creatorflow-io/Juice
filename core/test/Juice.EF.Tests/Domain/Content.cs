using System;
using Juice.Domain;

namespace Juice.EF.Tests.Domain
{
    public class Content : DynamicAuditEntity<Guid>
    {
        public Content(string code, string name)
        {
            Name = name;
            Code = code;
        }

        public string Code { get; private set; }
    }
}
