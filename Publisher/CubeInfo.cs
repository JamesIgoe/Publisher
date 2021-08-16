using System;
using System.Runtime.Serialization;

namespace Publisher
{
    [DataContract]
    public class CubeInfo
    {
        /// <summary>
        /// Object used by publisher to provide information to subscribing clients
        /// Contains 
        ///     cube server
        ///     cube Db
        ///     cube name
        ///     cube state
        ///     cube availability (used in load balancing)
        ///     last schema update
        /// </summary>

        private string _CubeHost;
        private string _CubeDb;
        private string _CubeName;
        private bool _IsActive;
        private bool _GiveToUser;
        private DateTime _LastSchemaChange;
        
        [DataMember()]
        public string CubeHost
        {
            get { return _CubeHost; }
            set { _CubeHost = value; }
        }

        [DataMember()]
        public string CubeDb
        {
            get { return _CubeDb; }
            set { _CubeDb = value; }
        }

        [DataMember()]
        public string CubeName
        {
            get { return _CubeName; }
            set { _CubeName = value; }
        }

        [DataMember()]
        public bool IsActive
        {
            get { return _IsActive; }
            set { _IsActive = value; }
        }

        [DataMember()]
        public bool GiveToUser
        {
            get { return _GiveToUser; }
            set { _GiveToUser = value; }
        }
        
        [DataMember()]
        public DateTime LastSchemaChange
        {
            get { return _LastSchemaChange; }
            set { _LastSchemaChange = value; }
        }
    }
}
