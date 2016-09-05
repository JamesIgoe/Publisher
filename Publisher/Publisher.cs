using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Threading;

[assembly: CLSCompliant(true)]
namespace Publisher
{
    public class UpdateEventArgs : EventArgs
    {
        private IList<CubeInfo> _Message;
        public IList<CubeInfo> Message
        {
            get { return _Message; }
            set { _Message = value; }
        }

        public System.Collections.IEnumerator GetEnumerator()
        {
            return _Message.GetEnumerator();
        }  
    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class Publisher : IPublisher, IDisposable
    {
        # region Constructor/Dispose

        public Publisher()
        {

        }

        public Publisher(string host, string port)
        {
            if (_Service == null)
            {
                _Service = new ServiceHost(typeof(Publisher));

                if (_Service.State != CommunicationState.Opened)
                {
                    //sets starting values for service
                    _Host = host;
                    _Port = port;
                    _EventSourceName = System.Configuration.ConfigurationManager.AppSettings["ServiceName"] as string;                    
                    _EventErrorValue = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["LogErrorForMOM"] as string);

                    //creates text log name value
                    _LogPath = System.Configuration.ConfigurationManager.AppSettings["LogPath"] as string;
                    _LogDefaultName = string.Format("{0}{1}",_LogPath, System.Configuration.ConfigurationManager.AppSettings["LogDefaultName"]);
                    _LogDefaultName = _LogDefaultName.Replace("%port%", _Port);
                    _LogDefaultName = _LogDefaultName.Replace("%yyyyMMddhhmmss%", DateTime.Now.ToString("yyyyMMddhhmmss"));

                    //creates message log and writes starting vlaues
                    _MessageLog = new MessageQueueLog(_LogDefaultName, EventLogEntryType.FailureAudit, _EventLogName, _EventSourceName, EventLogEntryType.Error, _EventErrorValue);
                    _MessageLog.Add(new Message(_EventSourceName, "localhost", EventLogEntryType.Information, "Set Host: " + _Host));
                    _MessageLog.Add(new Message(_EventSourceName, "localhost", EventLogEntryType.Information, "Set Port: " + _Port));

                    //starts process which checks the cube status tables
                    StartMonitorOnThread();

                    //starts services
                    StartNetTcpChannel(_Host, _Port);

                    //starts instance of slice load status class
                    _LoadData = new LoadStatusData(_MessageLog, _EventSourceName, _Host);
                }
            }
        }

        /// <summary>
        /// Dispose() calls Dispose(true) and GC.SuppressFinalize(this)
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Cleans up up managed objects
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.Stop();

                if (_MonitorThread != null)
                { 
                    if(_MonitorThread.IsAlive)
                    {
                        if (_Monitor != null)
                        {
                            _Monitor.Stop();
                            _Monitor = null;
                        }
                        _MonitorThread.Abort();
                        _MonitorThread = null;
                    }
                }
            }
        }

        # endregion

        # region Publisher/Subscriber implementation

        static List<ISubscriber> _CallbackList = new List<ISubscriber>();

        private static Monitor _Monitor = null;
        Thread _MonitorThread = null;

        private static IList<CubeInfo> _MessageList = null;
        private static IList<IList<CubeInfo>> _MessageListsToReturn = new List<IList<CubeInfo>>();
        private static int _MessageCount = 0;
        private static int _ReaderCount = 0;
        private static int _MessageListCounterForReturn = 0;
        
        private static string _Host;
        private static string _Port;

        private void StartMonitorOnThread()
        {
            try
            {
                _Monitor = new Monitor(_MessageLog);
                _Monitor.CubeStatusChangeSubscribe(SetInternalValuesAndNotify);

                ThreadStart ts = new ThreadStart(_Monitor.Start);
                _MonitorThread = new Thread(ts);
                _MonitorThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
                _MonitorThread.Start();
                _MessageLog.Add(new Message(_EventSourceName, _Host, EventLogEntryType.Information, "Started Monitor on thread"));
            }
            catch (Exception ex)
            {
                _MessageLog.Add(new Message(_EventSourceName, _Host, EventLogEntryType.Error, "Error in StartMonitorOnThread: " + ex.Message));
            }
        }

        public void Attach(string userId, string workstation, string application)
        {
            if (OperationContext.Current == null)
            {
                _MessageLog.Add(new Message(_EventSourceName, _Host, EventLogEntryType.Error, "No current context...."));                
            }
            else
            {
                ISubscriber callback = OperationContext.Current.GetCallbackChannel<ISubscriber>();

                lock (_CallbackList)
                {
                    if (!_CallbackList.Contains(callback))
                    {
                        _CallbackList.Add(callback);

                        _MessageLog.Add(new Message(userId, workstation, EventLogEntryType.Information, "User Attached with Version: " + userId + "," + workstation + "," + application));
                    }
                    else
                    {
                        _MessageLog.Add(new Message(userId, workstation, EventLogEntryType.Warning, "Client attempted duplicate attachment: " + userId + "," + workstation + "," + application));
                    }
                }
            }
        }

        public void Detach()
        {
            try
            {
                ISubscriber callback = OperationContext.Current.GetCallbackChannel<ISubscriber>();

                lock (_CallbackList)
                {
                    if (_CallbackList.Contains(callback))
                    {
                        _CallbackList.Remove(callback);
                        _MessageLog.Add(new Message(_EventSourceName, _Host, EventLogEntryType.Information, "User Detached"));
                    }
                    else
                    {
                        _MessageLog.Add(new Message(_EventSourceName, _Host, EventLogEntryType.Warning, "Invalide Operation - Callback does not exist to remove"));
                    }
                }
            }
            catch (Exception ex)
            {
                _MessageLog.Add(new Message(_EventSourceName, _Host, EventLogEntryType.Error, "Detach Failed: " + ex.Message));
            }
        }

        public void Notify()
        {
            _MessageLog.Add(new Message(_EventSourceName, _Host, EventLogEntryType.Information, "Starting Notification of Subscribers"));

            lock (_CallbackList)
            {
                //starts at top of collection and works backward to zero
                int countOfCallbacks = _CallbackList.Count;
                for (int counter = countOfCallbacks - 1; counter >= 0; counter--)
                {
                    //executes update, but if it errors, removes callback from collection
                    try
                    {
                        _CallbackList[counter].Update(GetNextCubeList());
                    }
                    catch
                    {
                        _CallbackList.RemoveAt(counter);
                        _MessageLog.Add(new Message(_EventSourceName, _Host, EventLogEntryType.Warning, "Removed Invalid Subscriber"));
                    }
                }
            }
            _MessageLog.Add(new Message(_EventSourceName, _Host, EventLogEntryType.Information, "Ended Notification of Subscribers"));
        }

        public void SendWorkbokForTracking(TrackedConnection item)
        {
            //unpacks and stores information
            try
            {
                string newWorkbook = item.UserID + "|" + item.Workstation + "|" + item.FullPath;
                _MessageLog.Add(new Message(_EventSourceName, _Host, EventLogEntryType.Information, "Workbook (User|HostName|FullPath): " + newWorkbook));
            }
            catch (Exception ex)
            {
                _MessageLog.Add(new Message(_EventSourceName, _Host, EventLogEntryType.Error, ex.Message));
            }
        }

        public IList<CubeInfo> GetAvailableCubeStatus()
        {
            return GetNextCubeList();
        }

        public CubeInfo GetActiveCube()
        {
            IList<CubeInfo> list = GetNextCubeList();
            CubeInfo newCube = null;

            foreach (CubeInfo cube in list)
            {
                if (cube.GiveToUser == true)
                {
                    newCube = cube;
                }
            }
            return newCube;
        }

        /// <summary>
        /// Uses string returned from GetLoadStatusViews executes SQL to get data
        /// </summary>
        /// <param name="viewDate"></param>
        /// <param name="viewRequested"></param>
        /// <returns></returns>
        public DataSet GetLoadStatusByDate(string viewDate)
        {
            return _LoadData.GetLoadStatusByDate(viewDate);
        }

        /// <summary>
        /// Uses string returned from GetLoadStatusViews executes SQL to get data
        /// </summary>
        /// <param name="viewDate"></param>
        /// <param name="viewRequested"></param>
        /// <returns></returns>
        public DataSet GetLoadStatusBySource(string viewDate)
        {
            return _LoadData.GetLoadStatusBySource(viewDate);
        }

        /// <summary>
        ///     Uses string returned from GetLoadStatusViews executes SQL to get data
        /// </summary>
        /// <param name="viewDate"></param>
        /// <param name="viewRequested"></param>
        /// <returns></returns>
        public DataSet GetLoadStatusByRegion(string viewDate)
        {
            return _LoadData.GetLoadStatusByRegion(viewDate);
        }

        # endregion

        # region Internal data management (list construction and allocation)

        static MessageQueueLog _MessageLog;
        static LoadStatusData _LoadData;

        private void SetInternalValuesAndNotify(IList<CubeInfo> message)
        {
            _MessageList = message;
            
            _MessageCount = _MessageList.Count;
            _ReaderCount = GetReaderCount();

            if (_MessageCount == 0)
            {
                //error that no rows returned
                _MessageLog.Add(new Message(_EventSourceName, _Host, EventLogEntryType.Error, "No cubes in cube list; no rows returned"));
            }
            else
            {
                if (_ReaderCount == 0)
                {
                    //error that no readers
                    _MessageLog.Add(new Message(_EventSourceName, _Host, EventLogEntryType.Error, "No readers in cube list"));
                }
             
                int[] readerIndex = CreateReaderCubeIndex();

                List<IList<CubeInfo>> messageListsToReturn = CreateMessageList(readerIndex);

                UpdateMessageList(messageListsToReturn);

                Notify();
            }
        }

        private static int GetReaderCount()
        {
            //get a count of readers
            int readerCount = 0;
            foreach (CubeInfo cube in _MessageList)
            {
                if (cube.IsActive == true)
                {
                    readerCount = readerCount + 1;
                }
            }
            return readerCount;
        }

        private static int[] CreateReaderCubeIndex()
        {
            //gets which are readers
            //set all items to writer
            //later turns one in each new list to reader
            int[] readerIndex = new int[_ReaderCount];
            int innerCounter = 0;
            for (int counter = 0; counter < _MessageCount; counter++)
            {
                if (_MessageList[counter].IsActive == true)
                {
                    readerIndex[innerCounter] = counter;
                    innerCounter = innerCounter + 1;

                    _MessageList[counter].GiveToUser = false;
                }
            }
            return readerIndex;
        }

        private static List<IList<CubeInfo>> CreateMessageList(int[] readerIndex)
        {
            //for each reader, create a list, add to lists to return to users in load-balanced way
            List<IList<CubeInfo>> messageListsToReturn = new List<IList<CubeInfo>>();

            if (_ReaderCount > 0)
            {
                for (int counter = 0; counter < _ReaderCount; counter++)
                {
                    IList<CubeInfo> listCopy = CreateNewListCopy();

                    //sets one to reader, based on list of reader indexes
                    listCopy[readerIndex[counter]].GiveToUser = true;
                    messageListsToReturn.Add(listCopy);
                }
            }
            else 
            {
                //if no readers, return a list of writers
                IList<CubeInfo> listCopy = CreateNewListCopy();
                messageListsToReturn.Add(listCopy);
            }

            return messageListsToReturn;
        }

        private static IList<CubeInfo> CreateNewListCopy()
        {
            IList<CubeInfo> listCopy = new List<CubeInfo>();

            //sets all to WRITER
            for (int counterInner = 0; counterInner < _MessageCount; counterInner++)
            {
                CubeInfo cubeCopy = new CubeInfo();
                cubeCopy.CubeHost = _MessageList[counterInner].CubeHost;
                cubeCopy.CubeDb = _MessageList[counterInner].CubeDb;
                cubeCopy.CubeName = _MessageList[counterInner].CubeName;
                cubeCopy.IsActive = _MessageList[counterInner].IsActive;
                cubeCopy.LastSchemaChange = _MessageList[counterInner].LastSchemaChange;
                cubeCopy.GiveToUser = false;

                listCopy.Add(cubeCopy);
            }
            return listCopy;
        }

        private static void UpdateMessageList(List<IList<CubeInfo>> messageListsToReturn)
        {
            lock (_MessageListsToReturn)
            {
                _MessageListCounterForReturn = 0;
                _MessageListsToReturn = messageListsToReturn;
            }
        }

        private IList<CubeInfo> GetNextCubeList()
        {   
            //using current list of list of cubes
            //return next in list fo lists
            //if at max, then set counter to 0, else increment one

            lock (this)
            {
                int counter = _MessageListCounterForReturn;

                if (_ReaderCount == 0)
                {
                    _MessageListCounterForReturn = 0;
                }
                else if (_MessageListCounterForReturn == (_ReaderCount - 1))
                {
                    _MessageListCounterForReturn = 0;
                }
                else
                {
                    _MessageListCounterForReturn = _MessageListCounterForReturn + 1;
                }
                return _MessageListsToReturn[counter];
            }           
        }

        # endregion

        # region Service-related Code

        static ServiceHost _Service = null;

        private static string _EventLogName = "Application";
        private static string _EventSourceName;
        string _LogDefaultName;
        string _LogPath;
        private static int _EventErrorValue;

        private void StartNetTcpChannel(string host, string port)
        {
            string serviceUrl = String.Format("net.tcp://{0}:{1}", host, port, CultureInfo.InvariantCulture);

            NetTcpBinding tcpBinding = new NetTcpBinding(SecurityMode.Transport);
            tcpBinding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Windows;

            tcpBinding.MaxReceivedMessageSize = int.MaxValue;
            tcpBinding.MaxBufferPoolSize = 9147483647;
            tcpBinding.MaxBufferSize = 2147483647;
            tcpBinding.OpenTimeout = TimeSpan.MaxValue;
            tcpBinding.ReceiveTimeout = TimeSpan.MaxValue;
            tcpBinding.SendTimeout = TimeSpan.MaxValue;

            System.Xml.XmlDictionaryReaderQuotas quotas = new System.Xml.XmlDictionaryReaderQuotas();
            quotas.MaxStringContentLength = int.MaxValue;
            quotas.MaxArrayLength = int.MaxValue;
            tcpBinding.ReaderQuotas = quotas;

            _Service.AddServiceEndpoint(typeof(IPublisher), tcpBinding, serviceUrl);
            _Service.Authorization.PrincipalPermissionMode = PrincipalPermissionMode.UseWindowsGroups;

            foreach (ServiceEndpoint ep in _Service.Description.Endpoints)
            {
                foreach (OperationDescription op in ep.Contract.Operations)
                {
                    DataContractSerializerOperationBehavior dataContractBehavior =
                       op.Behaviors.Find<DataContractSerializerOperationBehavior>()
                            as DataContractSerializerOperationBehavior;
                    if (dataContractBehavior != null)
                    {
                        dataContractBehavior.MaxItemsInObjectGraph = 200000000;
                    }
                }
            }

            ServiceThrottlingBehavior serviceThrottlingBehavior = new ServiceThrottlingBehavior();
            serviceThrottlingBehavior.MaxConcurrentSessions = int.MaxValue;
            serviceThrottlingBehavior.MaxConcurrentCalls = int.MaxValue;
            serviceThrottlingBehavior.MaxConcurrentInstances = int.MaxValue;
            _Service.Description.Behaviors.Add(serviceThrottlingBehavior);

            OpenChannel();
        }

        private static void OpenChannel()
        {
            try
            {
                _Service.Open();
                _MessageLog.Add(new Message(_EventSourceName, _Host, EventLogEntryType.Information, "Started NetTCP Channel"));
            }
            catch (Exception ex)
            {
                _MessageLog.Add(new Message(_EventSourceName, _Host, EventLogEntryType.Error, "Error in StartNetTcpChannel: " + ex.Message));
            }
        }

        private void Stop()
        {
            try
            {
                _Service.Close();
                _MessageLog.Add(new Message(_EventSourceName, _Host, EventLogEntryType.Information, "Stopped NetTCP Channel"));
            }
            catch (Exception ex)
            {
                _MessageLog.Add(new Message(_EventSourceName, _Host, EventLogEntryType.Error, "Error in Stop: " + ex.Message));
            }
        }

        # endregion
    }
}
