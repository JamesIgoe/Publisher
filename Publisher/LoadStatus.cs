using System.Data;
using System.Runtime.Serialization;

namespace Publisher
{
    [DataContract]
    public class LoadStatus
    {
        private DataTable _LoadStatusData;
        
        [DataMember()]
        public DataTable LoadStatusData
        {
            get { return _LoadStatusData; }
            set { _LoadStatusData = value; }
        }
    }
}
