using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Juice.Domain;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Juice.EF.Extensions
{
    public static class NotificationEntityTypeExtensions
    {
        public static bool IsNoticeFor(this IEntityType? entityType, EntityStates states)
        {
            var attr = entityType?.ClrType.GetCustomAttribute<NoticeAttribute>(true);
            return attr!=null  && attr.NoticeOn.HasFlag(states);
        }
    }
}
