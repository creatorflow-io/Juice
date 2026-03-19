using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Juice
{
    public interface IManagable
    {
        /// <summary>
        /// Gets a value indicating whether the current context is managed by the framework or infrastructure.
        /// </summary>
        bool IsManaged { get; }
    }
}
