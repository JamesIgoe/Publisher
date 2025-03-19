using System;
using System.Data;
using System.Data.OleDb;
using System.Diagnostics;

namespace Publisher
{
    public class LoadStatusData: IDisposable
    {
        # region Constructor and dispose

        public LoadStatusData(MessageQueueLog messageLog, string eventSourceName, string host)
        {
            _MessageLog = messageLog;
            _EventSourceName = eventSourceName;
            _Host = host;

            LoadSQLForSlices();

            LoadConnectionInfo();
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
                if (_Connection != null)
                {
                    _Connection.Dispose();
                    _Connection = null;
                }
            }
        }

        # endregion

        # region Public class methods and helpers

        /// <summary>
        /// Helper method for date replacement in SQL strings
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="viewDate"></param>
        /// <returns></returns>
        private static string ReplaceViewDate(string sql, string viewDate)
        {
            sql = sql.Replace("%ReplacementDate%", viewDate);
            return sql;
        }

        /// <summary>
        ///     Uses string returned from GetLoadStatusViews executes SQL to get data
        /// </summary>
        /// <param name="viewDate"></param>
        /// <param name="viewRequested"></param>
        /// <returns></returns>
        public DataSet GetLoadStatusByDate(DateTime viewDate)
        {
            // Convert DateTime to string in the format YYYYMMDD
            string viewDateString = viewDate.ToString("yyyy-MM-dd");
            string sql = ReplaceViewDate(_SliceDataSQLForDates, viewDateString);
            return ExecuteSQL(sql);
        }

        /// <summary>
        ///     Uses string returned from GetLoadStatusViews executes SQL to get data
        /// </summary>
        /// <param name="viewDate"></param>
        /// <param name="viewRequested"></param>
        /// <returns></returns>
        public DataSet GetLoadStatusBySource(DateTime viewDate)
        {
            // Convert DateTime to string in the format YYYYMMDD
            string viewDateString = viewDate.ToString("yyyy-MM-dd");
            string sql = ReplaceViewDate(_SliceDataSQLForDates, viewDateString);
            return ExecuteSQL(sql);
        }

        /// <summary>
        ///     Uses string returned from GetLoadStatusViews executes SQL to get data
        /// </summary>
        /// <param name="viewDate"></param>
        /// <param name="viewRequested"></param>
        /// <returns></returns>
        public DataSet GetLoadStatusByRegion(DateTime viewDate)
        {
            // Convert DateTime to string in the format YYYYMMDD
            string viewDateString = viewDate.ToString("yyyy-MM-dd");
            string sql = ReplaceViewDate(_SliceDataSQLForDates, viewDateString);
            return ExecuteSQL(sql);
        }
        #endregion

        # region Connection, SQL, related methods and fields

        private static string _SliceDataEnvironment;
        private static string _SliceDataSQLForDates;
        private static string _SliceDataSQLForSource;
        private static string _SliceDataSQLForRegion;

        private static MessageQueueLog _MessageLog = null;
        private static string _Host = string.Empty;
        private static string _EventSourceName = string.Empty;

        private static string _ConnectionString = null;
        private static string _DataSource = null;
        private static string _UserID = null;
        private static string _Password = null;
        private OleDbConnection _Connection = null;

        private static void LoadSQLForSlices()
        {
            try
            {
                _SliceDataEnvironment = System.Configuration.ConfigurationManager.AppSettings["SliceDataEnvironment"] as string;

                _SliceDataSQLForDates = System.Configuration.ConfigurationManager.AppSettings["SliceDataSQLForDates"] as string;
                _SliceDataSQLForDates = _SliceDataSQLForDates.Replace("%SliceDataEnvironment%", _SliceDataEnvironment);

                _SliceDataSQLForSource = System.Configuration.ConfigurationManager.AppSettings["SliceDataSQLForSource"] as string;
                _SliceDataSQLForSource = _SliceDataSQLForSource.Replace("%SliceDataEnvironment%", _SliceDataEnvironment);

                _SliceDataSQLForRegion = System.Configuration.ConfigurationManager.AppSettings["SliceDataSQLForRegion"] as string;
                _SliceDataSQLForRegion = _SliceDataSQLForRegion.Replace("%SliceDataEnvironment%", _SliceDataEnvironment);
            }
            catch (Exception ex)
            {
                _MessageLog.Add(new Message(_EventSourceName, _Host, EventLogEntryType.Error, "Error in LoadSQLForSlices: " + ex.Message));
            }
        }

        private static void LoadConnectionInfo()
        {
            try
            {
                _ConnectionString = System.Configuration.ConfigurationManager.AppSettings["OracleConnectionString"] as string;
                _DataSource = System.Configuration.ConfigurationManager.AppSettings["OracleDataSource"] as string;
                _UserID = System.Configuration.ConfigurationManager.AppSettings["OracleUserId"] as string;
                _Password = System.Configuration.ConfigurationManager.AppSettings["OraclePassword"] as string;
            }
            catch (Exception ex)
            {
                _MessageLog.Add(new Message(_EventSourceName, _Host, EventLogEntryType.Error, "Error in LoadConnectionInfo: " + ex.Message));
            }
        }

        private void Connect()
        {
            _ConnectionString = _ConnectionString.Replace("[DATASOURCE]", _DataSource);
            _ConnectionString = _ConnectionString.Replace("[USERID]", _UserID);
            _ConnectionString = _ConnectionString.Replace("[PASSWORD]", _Password); 
            
            try
            {
                _Connection = new OleDbConnection(_ConnectionString);
                _Connection.Open();
            }
            catch (Exception ex)
            {
                _MessageLog.Add(new Message(_EventSourceName, _Host, EventLogEntryType.Error, "Error in Connect: " + ex.Message));
            }
        }

        private void ConnectionClose()
        {
            if (_Connection != null)
            {
                if (_Connection.State == System.Data.ConnectionState.Open)
                {
                    _Connection.Close();
                }
                _Connection.Dispose();
            }
        }

        private DataSet ExecuteSQL(string sql)
        {
            Connect();
            DataSet ds = new DataSet();

            //execute sql
            if (_Connection != null)
            {
                if (_Connection.State == System.Data.ConnectionState.Open)
                {
                    try
                    {
                        OleDbDataAdapter da = new OleDbDataAdapter(sql, _Connection);
                        da.Fill(ds);
                    }
                    catch (Exception ex)
                    {
                        _Connection = null;
                        _MessageLog.Add(new Message(_EventSourceName, _Host, EventLogEntryType.Error, "Error in ExecuteSQL: " + ex.Message));
                    }
                    finally
                    {
                        //cleanup after each connection
                        //ConnectionClose();
                    }
                }
            }
            return ds;
        }

        # endregion
    }
}
