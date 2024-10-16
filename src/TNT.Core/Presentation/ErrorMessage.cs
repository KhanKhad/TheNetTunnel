using System;
using System.Text;
using TNT.Core.Exceptions.Remote;

namespace TNT.Core.Presentation
{
    public class ErrorMessage
    {
        public ErrorMessage() { }
        public ErrorMessage(short messageId, int askId, ErrorType type, string additionalExceptionInformation)
        {
            this.MessageId = messageId;
            this.AskId = askId;
            ErrorType = type;
            Exception = RemoteException.Create(type, additionalExceptionInformation, messageId, askId);
        }
       
        public short MessageId { get; set; }
        public int AskId { get; set; }
        public ErrorType ErrorType { get; set; }
        public string AdditionalExceptionInformation { get; set; }
        public  RemoteException Exception { get; }

        public override bool Equals(object obj)
        {
            return obj is ErrorMessage message &&
                   MessageId == message.MessageId &&
                   AskId == message.AskId &&
                   ErrorType == message.ErrorType &&
                   AdditionalExceptionInformation == message.AdditionalExceptionInformation;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(MessageId, AskId, ErrorType, AdditionalExceptionInformation);
        }

        public override string ToString()
        {
            StringBuilder ans = new StringBuilder();
            ans.Append($"Error: {ErrorType}");
            ans.Append($", Message type id: {MessageId}");
            ans.Append($", Ask id: {AskId}");

            if (!string.IsNullOrWhiteSpace(AdditionalExceptionInformation))
                ans.Append($". \"{AdditionalExceptionInformation}\".");
            return ans.ToString();
        }
    }
}