using System;
using System.Collections;
using System.Diagnostics;
using System.IO;

namespace Publisher
{
    public enum LogType
    { 
        Text,
        EventLog,
        Combined
    }

    public class MessageQueueLog : IDisposable
    {
        # region Properties
        
        /// <summary>
        /// Full file path for log
        /// </summary>
        private string _FileName = string.Empty;
        public string FileName
        {
            get { return _FileName; }
            set { _FileName = value; }
        }

        //general event log fields
        private string _LogName;
        private string _SourceName;
        private int _ErrorEventID;

        //Queues - one for event log and one for text log
        private Queue _EventMessageQueue = null;
        private Queue _EventQueueWrapper = null;
        private Queue _TextMessageQueue = null;
        private Queue _TextQueueWrapper = null;

        //general class variables - either Text, event, or combined log
        private LogType _LogType;

        //text logging componenets
        private StreamWriter _Result;
        private FileStream _Stream;
        
        private EventLogEntryType _EventEntryTypeThreshold;
        /// <summary>
        /// Error = 1
        /// Warning = 2
        /// Information = 4
        /// Success Audit = 8
        /// Failure Audit = 16
        /// </summary>
        public EventLogEntryType EventEntryTypeThreshold
        {
            get { return _EventEntryTypeThreshold; }
            set { _EventEntryTypeThreshold = value; }
        }

        private EventLogEntryType _TextEntryTypeThreshold;
        /// <summary>
        /// Error = 1
        /// Warning = 2
        /// Information = 4
        /// Success Audit = 8
        /// Failure Audit = 16
        /// </summary>
        public EventLogEntryType TextEntryTypeThreshold
        {
            get { return _TextEntryTypeThreshold; }
            set { _TextEntryTypeThreshold = value; }
        }

        # endregion

        # region Constructors - different depending on whether log is event log, text, or both

        /// <summary>
        /// constructor for text logging class
        /// </summary>
        /// <param name="file"></param>
        /// <param name="logtype"></param>
        public MessageQueueLog(string fileName, EventLogEntryType entryTypeThreshold)
        {
            _LogType = LogType.Text;
            _TextEntryTypeThreshold = entryTypeThreshold;
            _FileName = fileName;

            StartQueues();

            Start();
        }

        /// <summary>
        /// constructor for event logging class
        /// </summary>
        /// <param name="file"></param>
        /// <param name="logtype"></param>
        public MessageQueueLog(string logName, string sourceName, EventLogEntryType entryTypeThreshold, int errorEventId)
        {
            _LogType = LogType.EventLog;

            _LogName = logName;
            _SourceName = sourceName;
            _ErrorEventID = errorEventId;
            _EventEntryTypeThreshold = entryTypeThreshold;

            StartQueues();

            Start();
        }

        /// <summary>
        /// constructor for combined logging class
        /// </summary>
        /// <param name="file"></param>
        /// <param name="logtype"></param>
        public MessageQueueLog(string fileName, EventLogEntryType textEntryTypeThreshold, string logName, string sourceName, EventLogEntryType eventEntryTypeThreshold, int errorEventId)
        {
            _LogType = LogType.Combined;

            //value for text logging
            _FileName = fileName;
            _TextEntryTypeThreshold = textEntryTypeThreshold;

            //values for event log
            _ErrorEventID = errorEventId;
            _EventEntryTypeThreshold = eventEntryTypeThreshold;
            _LogName = logName;
            _SourceName = sourceName;

            StartQueues();

            Start();
        }

        private void StartQueues()
        {
            if (_LogType == LogType.Text)
            {
                StartTextMessageQueue();
            }
            else if (_LogType == LogType.EventLog)
            {
                StartEventMessageQueue();
            }
            else if (_LogType == LogType.Combined)
            {
                StartTextMessageQueue();
                StartEventMessageQueue();
            }
        }

        private void StartEventMessageQueue()
        {
            _EventMessageQueue = new Queue();
            _EventQueueWrapper = Queue.Synchronized(_EventMessageQueue);
        }

        private void StartTextMessageQueue()
        {
            _TextMessageQueue = new Queue();
            _TextQueueWrapper = Queue.Synchronized(_TextMessageQueue);
        }

        /// <summary>
        /// Callable method that constructs class for event, text, or both
        /// </summary>
        /// <param name="file"></param>
        /// <param name="logtype"></param>
        private void Start()
        {
            if (_LogType == LogType.Text)
            {
                StartTextLog();
            }
            else if (_LogType == LogType.EventLog)
            {
                StartEventLog();
            }
            else if (_LogType == LogType.Combined)
            {
                StartCombinedLog();
            }
            else
            {
                //need code for database connection and properties
            }
        }

        /// <summary>
        /// Method to start text log
        /// </summary>
        private void StartTextLog()
        {
            try
            {
                _Stream = new FileStream(_FileName, FileMode.Create, FileAccess.Write);
                _Result = new StreamWriter(_Stream);
                _Result.Close();
            }
            catch
            {
                //empty catch in case of conflicts
                //potential issues
                // - no directory rights
                // - no space
                // - invalid directory
            }
            finally
            {
            }
        }

        /// <summary>
        /// Method to start event log
        /// </summary>
        private void StartEventLog()
        {
            try
            {
                //verify access to log
                //TBD

                //verify or create event source
                if (!EventLog.SourceExists(_SourceName))
                {
                    EventLog.CreateEventSource(_SourceName, _LogName);
                }
            }
            catch
            {
                //empty catch in case of conflicts
                //potential issue: no rights to event log
            }
            finally
            {

            }
        }

        /// <summary>
        /// Starts both a text and an event log
        /// </summary>
        private void StartCombinedLog()
        {
            StartTextLog();
            StartEventLog();
        }

        /// <summary>
        /// Cleans up up managed objects
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_Stream != null)
                {
                    _Stream.Dispose();
                    _Stream = null;
                }

                if (_Result != null)
                {
                    _Result.Dispose();
                    _Result = null;
                }

                if (_EventMessageQueue != null)
                {
                    _EventMessageQueue = null;
                }

                if (_TextMessageQueue != null)
                {
                    _TextMessageQueue = null;
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

        # endregion

        # region Methods for adding messages to the queue, and writing logs

        private delegate void AddDelegate(Message message);
        
        /// <summary>
        /// Public method to add message to queue
        /// Will add to queue, text, event, or both, depending on starting parameters
        /// </summary>
        /// <param name="message"></param>
        public void Add(Message message)
        {
            if (message.IsValid())
            {
                if (_TextQueueWrapper != null)
                {
                    if (message.EventType <= _TextEntryTypeThreshold)
                    {
                        lock (_TextQueueWrapper)
                        {
                            _TextQueueWrapper.Enqueue(message);
                            Message msg = (Message)_TextQueueWrapper.Dequeue();

                            AddDelegate d = new AddDelegate(WriteTextLog);
                            d.Invoke(msg);                                
                        }
                    }
                }

                if (_EventQueueWrapper != null)
                {
                    if (message.EventType <= _EventEntryTypeThreshold)
                    {
                        lock (_EventQueueWrapper)
                        {
                            _EventQueueWrapper.Enqueue(message);
                            Message msg = (Message)_EventQueueWrapper.Dequeue();

                            AddDelegate d = new AddDelegate(WriteEventLog);
                            d.Invoke(msg);
                        }
                    }
                }

            }
        }

        ///// <summary>
        ///// Formatter for text message, prepends time stamp to each message
        ///// </summary>
        ///// <param name="message"></param>
        ///// <param name="level"></param>
        ///// <returns></returns>
        //private static string FormatMessagestring(string userID, string workstation, string excelVersion, string aRiskAddInInstalled)
        //{
        //    string messageFormatted = string.Format(CultureInfo.InvariantCulture, "{0}, {1}, {2}, {3}, {4}", System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), userID, workstation, excelVersion, aRiskAddInInstalled);
        //    return messageFormatted;
        //}

        /// <summary>
        /// Method to get EventLogEntryType as a string
        /// </summary>
        /// <param name="errorLevel"></param>
        /// <returns></returns>
        public static EventLogEntryType GetEventLogEntryType(string errorLevel)
        {
            switch (errorLevel)
            { 
                case "Error":
                    return EventLogEntryType.Error;
                case "Warning":
                    return EventLogEntryType.Warning;
                case "Information":
                    return EventLogEntryType.Information;
                case "SuccessAudit":
                    return EventLogEntryType.SuccessAudit;
                case "FailureAudit":
                    return EventLogEntryType.FailureAudit;
                default:
                    return EventLogEntryType.Error;
            }
        }

        /// <summary>
        /// Specific method to write the text log
        /// </summary>
        /// <param name="message"></param>
        private void WriteTextLog(Message message)
        {
            try
            {
                _Stream = new FileStream(_FileName, FileMode.Append, FileAccess.Write);
                _Result = new StreamWriter(_Stream);
                string newLine = string.Format("{0}~{1}~{2}", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"), message.EventType, message.Description);
                _Result.WriteLine(newLine);
                _Result.Close();
            }
            catch
            {
                //empty catch in case of conflicts
                //potential issues
                // - no directory rights
                // - no space
                // - invalid directory
            }
            finally
            {

            }
        }

        /// <summary>
        /// Specific method to write the event log
        /// </summary>
        /// <param name="message"></param>
        private void WriteEventLog(Message message)
        {
            try
            {
                int eventId = 0;
                if (message.EventType == EventLogEntryType.Error)
                {
                    eventId = _ErrorEventID;
                }
                EventLog.WriteEntry(_SourceName, message.ToString(), message.EventType, eventId);
            }
            catch
            {
                //empty in case there are unexpected issues writing to event viewer
            }
        }

        # endregion
    }
}
