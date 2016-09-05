using System;
using System.Collections;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;
using System.Data;

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
