using System;
using System.Collections.Generic;
using System.Text;

namespace Valkyrie.Core
{
    interface IValkyrie
    {
        Task Run(CancellationToken token);
    }
}
