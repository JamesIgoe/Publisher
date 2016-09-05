using System.Runtime.Serialization;

namespace Publisher
{
    [DataContract]
    public class TrackedConnection
    {
        private string _Key = string.Empty;
        private string _UserID = string.Empty;
        private string _Workstation = string.Empty;
        private string _FullPath = string.Empty;
        private bool _SentToServer = false;

        [DataMember()]
        public string Key
        {
            get { return _Key; }
            set { _Key = value; }
        }

        [DataMember()]
        public string UserID
        {
            get { return _UserID; }
            set { _UserID = value; }
        }

        [DataMember()]
        public string Workstation
        {
            get { return _Workstation; }
            set { _Workstation = value; }
        }

        [DataMember()]
        public string FullPath
        {
            get { return _FullPath; }
            set { _FullPath = value; }
        }

        [DataMember()]
        public bool SentToServer
        {
            get { return _SentToServer; }
            set { _SentToServer = value; }
        }

        public override string ToString()
        {
            return string.Format("{0}{1}{2}", _UserID, _Workstation, _FullPath);
        }

        public string GetKey()
        {
            return this.ToString();
        }
    }
}
