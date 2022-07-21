using System;
using Juice.Domain;

namespace Juice.EF.Test.Domain
{
    public class Content : DynamicEntity<Guid>
    {
        public Content(string code, string name) : base(name)
        {
            Code = code;
        }

        public string Code { get; private set; }
    }
}
