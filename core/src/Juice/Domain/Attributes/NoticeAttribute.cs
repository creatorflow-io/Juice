using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Juice.Domain
{
    /// <summary>
    /// Fires event when an entity was tracked as added, modified or deleted.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    public class NoticeAttribute : Attribute
    {
        public NoticeAttribute(EntityStates noticeOn)
        {
            NoticeOn = noticeOn;
        }
        public NoticeAttribute()
        {
            NoticeOn = EntityStates.Created | EntityStates.Modified | EntityStates.Deleted;
        }
        public EntityStates NoticeOn { get; }
    }
}
