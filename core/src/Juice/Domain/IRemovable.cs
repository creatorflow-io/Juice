using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Juice.Domain
{
    public interface IRemovable
    {
        bool IsRemoved { get; }
        string? RemovedUser { get; }
        DateTimeOffset? RemovedDate { get; }

        string? RestoredUser { get; }
        DateTimeOffset? RestoredDate { get; }
    }
}
