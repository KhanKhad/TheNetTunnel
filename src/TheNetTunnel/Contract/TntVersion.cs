using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheNetTunnel.Contract
{
    /// <summary>
    /// Marks a contract interface with the id used to distinguish it from other
    /// contracts sharing the same port. When the attribute is omitted the default
    /// value <see cref="TheNetTunnel.Presentation.InterlocutorProperties.DefaultContractId"/> (255) is used.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = true)]
    public class TntContractId : Attribute
    {
        public TntContractId(byte contractId)
        {
            ContractId = contractId;
        }

        public byte ContractId { get; }
    }

    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = true)]
    public class TntClientVersion : Attribute
    {
        private readonly Version _version;

        public TntClientVersion(int major, int minor, int build)
        {
            _version = new Version(major, minor, build);
        }

        public Version Version => _version;
    }

    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = true)]
    public class TntServerVersion : Attribute
    {
        private readonly Version _version;

        public TntServerVersion(int major, int minor, int build)
        {
            _version = new Version(major, minor, build);
        }

        public Version Version => _version;
    }


    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = true)]
    public class TntMinimalClientVersion : Attribute
    {
        private readonly Version _version;

        public TntMinimalClientVersion(int major, int minor, int build)
        {
            _version = new Version(major, minor, build);
        }

        public Version Version => _version;
    }

    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = true)]
    public class TntMinimalServerVersion : Attribute
    {
        private readonly Version _version;

        public TntMinimalServerVersion(int major, int minor, int build)
        {
            _version = new Version(major, minor, build);
        }

        public Version Version => _version;
    }
}
