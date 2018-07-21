using System.Diagnostics;
using System.Globalization;

namespace Publisher
{
    public static class StaticEventLog
    {
        public static void WriteErrorToApplicationLog(string sourceName, string newLine)
        {
            WriteLogToEventViewer(sourceName, "Application", EventLogEntryType.Error, newLine, 4000);
        }
        
        public static void WriteInformationToApplicationLog(string sourceName, string newLine)
        {
            WriteLogToEventViewer(sourceName, "Application", EventLogEntryType.Information, newLine, 0);
        }

        /// <summary>
        /// Primary method for writing event viewer messages. 
        /// Marked as static and does not require instantiation
        /// </summary>
        /// <param name="NewLine"></param>
        /// <param name="level"></param>
        public static void WriteLogToEventViewer(string sourceName, string logName, EventLogEntryType type, string newLine, int eventId)
        {
            string sEvent = string.Empty;
            
            try
            {
                sEvent = string.Format(CultureInfo.InvariantCulture, "{0}", newLine);

                if (!EventLog.SourceExists(sourceName))
                {
                    EventLog.CreateEventSource(sourceName, logName);
                }
                EventLog.WriteEntry(sourceName, sEvent, type, eventId);
            }
            catch
            {
                //empty in case there are unexpected issues writing to event viewer
            }
        }
    }
}
