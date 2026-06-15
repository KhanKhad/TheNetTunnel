using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheNetTunnel.Exceptions.Remote
{
    public class RemoteContractIdIsNotSupported : RemoteException
    {
        public RemoteContractIdIsNotSupported(short? messageId, int? askId, string message = null)
            : base(ErrorType.ContractIdIsNotSupported, isFatal: true, messageId, askId, message)
        {
        }
    }
}
