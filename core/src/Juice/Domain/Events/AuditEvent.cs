using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Juice.Domain.Events
{

    public class AuditEvent : DataEvent
    {
        public AuditEvent(string name) : base(name)
        {
        }
        public override bool IsAudit => true;
    }

    /// <summary>
    /// Generic data event for entity, use for audit
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class AuditEvent<T> : AuditEvent
    {
        public AuditEvent(string name) : base(name)
        {
        }
    }

}
