using System.Diagnostics;
using System.Globalization;

namespace Publisher
{
    /// <summary>
    /// Object used by logging class to standardize information for MessageQueueLog
    /// Contains 
    ///     _User
    ///     _Workstation
    ///     _Description
    ///     _EventType
    ///     _Description
    ///     String override to convert message to string
    /// </summary>
    public class Message
    {
        public Message(string user, string workstation, EventLogEntryType eventType, string description)
        {
            _User = user;
            _Workstation = workstation;
            _EventType = eventType;
            _Description = description;
        }

        public bool IsValid()
        {
            bool isValid = true;

            //if all values are longe rthan zero, valid

            return isValid;
        }
        
        EventLogEntryType _EventType;
        public EventLogEntryType EventType
        {
            get { return _EventType; }
            set { _EventType = value; }
        }
        
        string _User;
        public string User
        {
            get { return _User; }
            set { _User = value; }
        }

        string _Workstation;
        public string Workstation
        {
            get { return _Workstation; }
            set { _Workstation = value; }
        }

        string _Description;
        public string Description
        {
            get { return _Description; }
            set { _Description = value; }
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} - {1}: {2}", _User, _Workstation, _Description);
        }
    }
}
