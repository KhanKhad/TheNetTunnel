using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TNT.Core.Contract
{
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = true)]
    public class TntClientVersion : Attribute
    {
        private readonly string _version;

        public TntClientVersion(string version)
        {
            _version = version;
        }

        public string Version => _version;
    }

    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = true)]
    public class TntServerVersion : Attribute
    {
        private readonly string _version;

        public TntServerVersion(string version)
        {
            _version = version;
        }

        public string Version => _version;
    }


    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = true)]
    public class TntMinimalClientVersion : Attribute
    {
        private readonly string _version;

        public TntMinimalClientVersion(string version)
        {
            _version = version;
        }

        public string Version => _version;
    }

    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = true)]
    public class TntMinimalServerVersion : Attribute
    {
        private readonly string _version;

        public TntMinimalServerVersion(string version)
        {
            _version = version;
        }

        public string Version => _version;
    }
}
