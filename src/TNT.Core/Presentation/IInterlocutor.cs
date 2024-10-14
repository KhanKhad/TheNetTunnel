using System;
using System.Reflection;
using System.Threading.Tasks;
using TNT.Core.Exceptions.Local;

namespace TNT.Core.Presentation
{
    /*
     *  DO NOT RENAME OR CHANGE SIGNATURE OF ANY METHOD!!!!
     *  IT USES DURING CONTRACT EMIT PROCESS
     */

    /// <summary>
    /// Incapsulates interaction with remote contract
    /// </summary>
    public interface IInterlocutor
    {
        /// <summary>
        /// Sends "Say" message with "values" arguments
        /// </summary>
        ///<exception cref="ArgumentException">wrong message id</exception>
        ///<exception cref="ConnectionIsLostException"></exception>
        ///<exception cref="LocalSerializationException">some of the argument type serializers is not implemented, or not the same as specified in the contract</exception>
        void Say(int messageId, object[] values);


        Task SayAsync(int messageId, object[] values);
        /// <summary>
        /// Remote method call. Blocking.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        ///<exception cref="ArgumentException">wrong message id</exception>
        ///<exception cref="TntCallException"></exception>
        ///<exception cref="LocalSerializationException">some of the argument type serializers or deserializers are not implemented, 
        /// or not the same as specified in the contract</exception>
        T Ask<T>(int messageId, object[] values);


        Task<T> AskAsync<T>(int messageId, object[] values);
    }
}