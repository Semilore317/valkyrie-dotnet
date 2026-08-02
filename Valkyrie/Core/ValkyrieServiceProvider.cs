using System;
using System.Collections.Generic;
using System.Text;

namespace Valkyrie.Core
{
    public static class ValkyrieServiceProvider
    {
        // static classes will be created at either the start of the application or when the class is first used
        public static IServiceProvider? ServiceProvider { get; set; }
    }
}
