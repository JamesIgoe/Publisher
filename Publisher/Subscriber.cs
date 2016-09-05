using System;
using System.Data;
using System.Net;
using System.ServiceModel;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;


namespace Publisher
{
    # region Project-level delegates for updating clients

    public delegate void UpdateUI(IList<CubeInfo> message);
    public delegate void UpdateStatus();

    # endregion

    /// <summary>
    /// Struct for passing parameters to Subscriber
    /// </summary>
    public struct SubscriberParameters
    { 
        public UpdateUI AppDelegate;
        public UpdateStatus StatusDelegate;
        public string Host;
        public string Port;
        public string UserId;
        public string Workstation;
        public string Application;
        public string Title;
        public MessageQueueLog MessageLog;
    }
    
    public class Subscriber : ISubscriber, IDisposable
    {
        # region Properties and Delegates

        IPublisher _Publisher = null;
        DuplexChannelFactory<IPublisher> _Channel;

        UpdateUI _AppDelegate;
        UpdateStatus _StatusDelegate;

        private TrackedConnections _Tracker = null;
        public TrackedConnections Tracker
        {
            get { return _Tracker; }
            set { _Tracker = value; }
        }

        string _UserId;
        public string UserId
        {
            get { return _UserId; }
            set { _UserId = value; }
        }

        string _Workstation;
        public string Workstation
        {
            get { return _Workstation; }
            set { _Workstation = value; }
        }

        string _Application;
        public string Application
        {
            get { return _Application; }
            set { _Application = value; }
        }

        private string _EventSourceName;

        private string _Title;
        public string Title
        {
            get { return _Title; }
            set { _Title = value; }
        }
        
        private string _Host;
        public string Host
        {
            get { return _Host; }
            set { _Host = value; }
        }

        private string _Port;
        public string Port
        {
            get { return _Port; }
            set { _Port = value; }
        }

        private bool _IsConnected;
        public bool IsConnected
        {
            get { return _IsConnected; }
        }

        private int _SleepForRecheckInMillisecondsBase = 1000;
        private int _SleepForRecheckInMillisecondsCurrent = 1;
        private int _SleepForRecheckInMillisecondsMax = 360000;
        /// <summary>
        /// Increases sleep for rety to connect to publisher
        /// Starts at 1 second, increasing up to the maximum, at whihc it stays
        /// </summary>
        /// <returns></returns>
        private int NextPollingFrequncy()
        {
            if (!_IsDisposing)
            {
                if (_SleepForRecheckInMillisecondsCurrent < _SleepForRecheckInMillisecondsBase)
                {
                    _SleepForRecheckInMillisecondsCurrent = _SleepForRecheckInMillisecondsBase;
                }
                else if (_SleepForRecheckInMillisecondsCurrent < _SleepForRecheckInMillisecondsMax)
                {
                    _SleepForRecheckInMillisecondsCurrent = _SleepForRecheckInMillisecondsCurrent * 2;
                }
                else
                {
                    _SleepForRecheckInMillisecondsCurrent = _SleepForRecheckInMillisecondsMax;
                }

                //final check to make sure not more than max
                if (_SleepForRecheckInMillisecondsCurrent > _SleepForRecheckInMillisecondsMax)
                {
                    _SleepForRecheckInMillisecondsCurrent = _SleepForRecheckInMillisecondsMax;
                }
            }
            else
            {
                _SleepForRecheckInMillisecondsCurrent = 0;
            }

            return _SleepForRecheckInMillisecondsCurrent;
        }

        private IList<CubeInfo> _Message;
        public IList<CubeInfo> Message
        {
            get { return _Message; }
            set { _Message = value; }
        }

        # endregion

        # region Constructor and Dispose

        /// <summary>
        /// Method for use by legacy clients to call methods for cube status, but receive update notifications
        /// </summary>
        /// <param name="appDelegate"></param>
        /// <param name="host"></param>
        /// <param name="port"></param>
        public Subscriber(UpdateUI appDelegate, string host, string port)
        {
            _AppDelegate = appDelegate;

            _Host = host;
            _Port = port;

            _EventSourceName = System.Configuration.ConfigurationManager.AppSettings["ServiceName"] as string;
        }

        ///// <summary>
        ///// Method for use by legacy clients to call methods for cube status without subscribing
        ///// </summary>
        ///// <param name="host"></param>
        ///// <param name="port"></param>
        public Subscriber(string host, string port)
        {
            _Host = host;
            _Port = port;

            _EventSourceName = System.Configuration.ConfigurationManager.AppSettings["ServiceName"] as string;
        }

        MessageQueueLog _MessageLog = null;
        /// <summary>
        /// Standard client for Excel to pass in numerous parameter values, with two delegates
        /// One delegate subscribes to changes in cude lists
        /// Second delegate subscribes to changes in cube status
        /// </summary>
        /// <param name="parameters"></param>
        public Subscriber(SubscriberParameters parameters)
        {
            _AppDelegate = parameters.AppDelegate;
            _StatusDelegate = parameters.StatusDelegate;

            _Host = parameters.Host;
            _Port = parameters.Port;
            _UserId = parameters.UserId;
            _Workstation = parameters.Workstation;
            _Application = parameters.Application;
            _Title = parameters.Title;
            _MessageLog = parameters.MessageLog;

            _EventSourceName = System.Configuration.ConfigurationManager.AppSettings["ServiceName"] as string;

            _Tracker = new TrackedConnections(this);
        }

        private bool _IsDisposing = false;

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
            try
            {
                //items to prevent faulted event fromfirigin after dispose
                //sets valus for class to know it hsould not continue other processes
                //then removes faulted event, so it will not refire
                //then sets counter to zero so faulted event ends immediately
                _IsDisposing = true;
                _SleepForRecheckInMillisecondsCurrent = 0;

                if (_Publisher != null)
                {
                    try
                    {
                        ((ICommunicationObject)_Publisher).Faulted -= new EventHandler(ClientFaulted);
                    }
                    catch
                    {
                        //publisher faulted, so ignore
                    }

                    try
                    {
                        if (this._IsConnected)
                        {
                            _Publisher.Detach();
                        }
                    }
                    catch
                    {
                        //publisher faulted, so ignore
                    }
                    _Publisher = null;
                } 
                
                try
                {
                    //close gracefully
                    _Channel.Close();
                }
                catch
                {
                    //publisher faulted
                    //close ungracefully
                    _Channel.Abort();
                }
                _Channel = null;

                GC.Collect();
            }
            catch
            {
                //item no longer exists....
            }
        }

        # endregion

        # region Methods

        /// <summary>
        /// Typical subscriber method for Excel clients, which requires appriate delegates are passed
        /// </summary>
        public void Run()
        {
            if (!_IsDisposing)
            {
                Attach(_UserId, _Workstation, _Application);

                GetCurrentCubes();

                //updates status delegate for subscriber last, in case there is error in task panel code
                UpdateStatus(true);
            }
        }

        /// <summary>
        /// Private method for getting list of current cubes
        /// </summary>
        private void GetCurrentCubes()
        {
            try
            {
                IList<CubeInfo> currentCubes = _Publisher.GetAvailableCubeStatus();
                if (currentCubes != null && currentCubes.Count > 0)
                {
                    this.Update(currentCubes);
                }
            }
            catch (Exception ex)
            {
                WriteMessageLog(new Message(_UserId, _Workstation, EventLogEntryType.Error, "Error in GetCurrentCubes: " + ex.Message));
            }
        }

        /// <summary>
        /// Primary subscriber method for Excel clients, which requires appriate delegates are passed
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="workstation"></param>
        /// <param name="application"></param>
        public void Attach(string userId, string workstation, string application)
        {
            try
            {
                CreateNetTcpChannel(_Host, _Port);

                _Publisher.Attach(userId, workstation, application);

                UpdateStatus(true);
            }
            catch (Exception ex)
            {
                WriteMessageLog(new Message(_UserId, _Workstation, EventLogEntryType.Error, "Error in Attach: " + ex.Message));
            }
        }

        /// <summary>
        /// Method required by Publisher, to return list fo cubes
        /// </summary>
        /// <param name="message"></param>
        public void Update(IList<CubeInfo> message)
        {
            if (message != null)
            {
                _Message = message;

                _AppDelegate(message);
            }
        }

        /// <summary>
        /// Updates client if publsiher (service) is down
        /// </summary>
        /// <param name="isConnected"></param>
        private void UpdateStatus(bool isConnected)
        {
            _IsConnected = isConnected;
            if (_StatusDelegate != null)
            {
                _StatusDelegate();
            }
        }
                
        /// <summary>
        /// Send workbook information to serivce
        /// </summary>
        /// <param name="item"></param>
        public void SendWorkbokForTracking(TrackedConnection item)
        {
            try
            {
                if (_IsConnected == true)
                {
                    _Publisher.SendWorkbokForTracking(item);
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            catch (Exception ex)
            {
                _MessageLog.Add(new Message(_EventSourceName, _Host, EventLogEntryType.Error, ex.Message));
            }
        }

        /// <summary>
        /// Returns list fo all cubes and status
        /// </summary>
        /// <returns></returns>
        public IList<CubeInfo> GetAvailableCubeStatus()
        {
            if (_IsConnected == true)
            {
                return _Publisher.GetAvailableCubeStatus();
            }
            else
            {
                throw new InvalidOperationException("Publisher is not connected.");
            }
        }

        /// <summary>
        /// Returns single active cube
        /// Load-balanced by server if there is more than 1 active cube
        /// </summary>
        /// <returns></returns>
        public CubeInfo GetActiveCube()
        {
            if (_IsConnected == true)
            {
                return _Publisher.GetActiveCube();
            }
            else
            {
                throw new InvalidOperationException("Publisher is not connected.");
            }
        }

        /// <summary>
        /// Uses string returned from GetLoadStatusViews executes SQL to get data
        /// </summary>
        /// <param name="viewDate"></param>
        /// <param name="viewRequested"></param>
        /// <returns></returns>
        public DataSet GetLoadStatusByDate(string viewDate)
        {
            if (_IsConnected == true)
            {
                return _Publisher.GetLoadStatusByDate(viewDate);
            }
            else
            {
                throw new InvalidOperationException("Publisher is not connected.");
            }
        }

        /// <summary>
        /// Uses string returned from GetLoadStatusViews executes SQL to get data
        /// </summary>
        /// <param name="viewDate"></param>
        /// <param name="viewRequested"></param>
        /// <returns></returns>
        public DataSet GetLoadStatusBySource(string viewDate)
        {
            if (_IsConnected == true)
            {
                return _Publisher.GetLoadStatusBySource(viewDate);
            }
            else
            {
                throw new InvalidOperationException("Publisher is not connected.");
            }
        }

        /// <summary>
        ///     Uses string returned from GetLoadStatusViews executes SQL to get data
        /// </summary>
        /// <param name="viewDate"></param>
        /// <param name="viewRequested"></param>
        /// <returns></returns>
        public DataSet GetLoadStatusByRegion(string viewDate)
        {
            if (_IsConnected == true)
            {
                return _Publisher.GetLoadStatusByRegion(viewDate);
            }
            else
            {
                throw new InvalidOperationException("Publisher is not connected.");
            }
        }

        # endregion

        # region Service code

        /// <summary>
        /// Creates duplex channel to service
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        private void CreateNetTcpChannel(string host, string port)
        {
            string enpointAddress = String.Format("net.tcp://{0}:{1}", host, port);
            
            Uri uri = new Uri(enpointAddress);
            string serviceName = uri.LocalPath.Contains("/") ? uri.LocalPath.Replace("/", string.Empty) : uri.LocalPath;

            string spnName = string.Format("{0}/{1}", serviceName, uri.Host);

            NetTcpBinding netTcpbinding = new NetTcpBinding(SecurityMode.Transport);
            netTcpbinding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Windows;

            netTcpbinding.MaxReceivedMessageSize = int.MaxValue;
            netTcpbinding.MaxBufferPoolSize = 9147483647;
            netTcpbinding.MaxBufferSize = 2147483647;
            netTcpbinding.OpenTimeout = TimeSpan.MaxValue;
            netTcpbinding.ReceiveTimeout = TimeSpan.MaxValue;
            netTcpbinding.SendTimeout = TimeSpan.MaxValue;

            System.Xml.XmlDictionaryReaderQuotas quotas = new System.Xml.XmlDictionaryReaderQuotas();
            quotas.MaxStringContentLength = int.MaxValue;
            quotas.MaxArrayLength = int.MaxValue;
            netTcpbinding.ReaderQuotas = quotas;

            EndpointAddress endpointAddress = new EndpointAddress(uri, new SpnEndpointIdentity(spnName));

            _Channel = new DuplexChannelFactory<IPublisher>(this, netTcpbinding, endpointAddress);

            _Channel.Credentials.Windows.ClientCredential = (NetworkCredential)CredentialCache.DefaultCredentials;

            try
            {
                _Publisher = _Channel.CreateChannel();
                ((ICommunicationObject)_Publisher).Faulted += new EventHandler(ClientFaulted);
                _IsConnected = true;
                WriteMessageLog(new Message(_UserId, _Workstation, EventLogEntryType.SuccessAudit, "Attached to service"));
            }
            catch (Exception ex)
            {
                WriteMessageLog(new Message(_UserId, _Workstation, EventLogEntryType.Error, "Error creating channel to service: " + ex.Message));
            }
        }

        private void WriteMessageLog(Message msg)
        {
            if (_MessageLog != null)
            {    
                try
                {
                    _MessageLog.Add(msg);
                }
                catch
                {
                    //nothing to do
                }
            }
        }

        /// <summary>
        /// Method to reattach to service if disconnected
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClientFaulted(object sender, EventArgs e)        
        {
            //while (!_IsDisposing)
            //{
                //executes delegate to write back to client, notify of status change
                UpdateStatus(false);

                //IN CASE OF WCF COMMUNICATION ERROR ABORT THE EXISTING CHANNEL AND CREATE A NEW ONE
                try
                {
                    WriteMessageLog(new Message(_UserId, _Workstation, EventLogEntryType.FailureAudit, "Client reattaching"));

                    //attempt to prevent hanging...
                    if (_Publisher != null)
                    {
                        ((ICommunicationObject)sender).Abort();
                        _Publisher = null;
                    }

                    int sleepTime = NextPollingFrequncy();
                    SleepThis(sleepTime);

                    if (!_IsDisposing)
                    {
                        Run();
                    }
                }
                catch (Exception ex)
                {
                    WriteMessageLog(new Message(_UserId, _Workstation, EventLogEntryType.Error, "Client faulted, unable to Abort and Re-run: " + ex.Message));
                }
            //}
        }

        private void SleepThis(int forMilliseconds)
        {
            int localMillisecondsSlice = 10;

            for (int counter = localMillisecondsSlice; counter < forMilliseconds; counter = counter + localMillisecondsSlice)
            {
                if (!_IsDisposing)
                {
                    Thread.Sleep(localMillisecondsSlice);
                }
            }

        }

        # endregion
    }
}
