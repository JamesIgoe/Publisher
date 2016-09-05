using System;
using System.Threading;
using System.Data.OleDb;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.Reflection;

namespace Publisher
{
    /// <summary>
    /// Class is used to loop continually to return SQL of cube status to publisher
    /// If cube returned differed from piro cube, launches delegate to notifiy client of updated cube statuses
    /// </summary>
    public class Monitor : IDisposable
    {
        # region Constructor/Dispose and class-level fields

        //values for connection
        OleDbConnection _Connection = null;
        string _ConnectionString = null;
        string _DataSource = null;
        string _UserID = null;
        string _Password = null;
        string _CubeStatusSQL = null;
        string _ConcatenatedString = string.Empty;

        IList<CubeInfo> _CubeList = null;

        MessageQueueLog _MessageLog = null;
        
        private int _PollingInterval = 20000;
        /// <summary>
        /// Constructor reads variables from app config to sett parameters for monitoring process
        /// </summary>
        public Monitor(MessageQueueLog messageLog)
        {
            _MessageLog = messageLog;

            _ConnectionString = System.Configuration.ConfigurationManager.AppSettings["OracleConnectionString"] as string;
            _DataSource = System.Configuration.ConfigurationManager.AppSettings["OracleDataSource"] as string;
            _UserID = System.Configuration.ConfigurationManager.AppSettings["OracleUserId"] as string;
            _Password = System.Configuration.ConfigurationManager.AppSettings["OraclePassword"] as string;
            _CubeStatusSQL = System.Configuration.ConfigurationManager.AppSettings["CubeStatusSQL"] as string;

            _PollingInterval = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["ServerPollingFrequencyInMilliseconds"], CultureInfo.InvariantCulture);
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

        # region Delegate

        /// <summary>
        /// Delegate for updating connection state in client, e.g., publisher
        /// </summary>
        public delegate void CubeStatus(IList<CubeInfo> update);
        event CubeStatus CubeStatusChange;
        public void CubeStatusChangeSubscribe(CubeStatus eventHandler)
        {
            lock (this)
            {
                CubeStatusChange += new CubeStatus(eventHandler);
            }
        }

        /// <summary>
        /// method invoked to update connection state in 
        /// </summary>
        /// <param name="state"></param>
        private void CubeStatusChangeUpdate(IList<CubeInfo> update)
        {
            if (CubeStatusChange != null)
            {
                CubeStatusChange(update);
            }
        }

        # endregion

        # region Process code - Connection, execution, comparison

        /// <summary>
        /// Method to start polling for cubes changes
        /// </summary>
        public void Start()
        {
            _Stop = false;
            BeginPolling();
        }

        bool _Stop = false;
        /// <summary>
        /// Method to set property to stop monitor
        /// </summary>
        public void Stop()
        {
            _Stop = true;
        }

        /// <summary>
        /// Primary internal method for looping through SQL and cube status
        /// Launches delegate if cubes have changed
        /// </summary>
        private void BeginPolling()
        {
            try
            {
                while (_Stop == false)
                {
                    Connect();

                    //execute sql
                    if (_Connection != null)
                    {
                        if (_Connection.State == System.Data.ConnectionState.Open)
                        {
                            try
                            {
                                //create and execute command object
                                OleDbCommand command = this._Connection.CreateCommand();
                                command.CommandText = this._CubeStatusSQL;
                                OleDbDataReader reader = command.ExecuteReader();

                                //for each item returned, create cubeInfo object and add to list
                                string concatenatedString = string.Empty;

                                if (reader != null)
                                {
                                    concatenatedString = ConcatenateItem(reader, concatenatedString);

                                    if (_ConcatenatedString.ToUpperInvariant() != concatenatedString.ToUpperInvariant())
                                    {
                                        _ConcatenatedString = concatenatedString;

                                        PopulateCubeList(concatenatedString);

                                        //event for update delegate
                                        CubeStatusChangeUpdate(_CubeList);
                                    }

                                    reader.Close();
                                    reader.Dispose();
                                    command.Dispose();
                                }
                                else 
                                {
                                    _MessageLog.Add(new Message("Publisher Service", "localhost", EventLogEntryType.Error, "Error in BeginPolling - Reader is null; no information returned from server"));
                                }
                            }
                            catch (Exception ex)
                            {
                                _Connection = null;
                                _MessageLog.Add(new Message("Publisher Service", "localhost", EventLogEntryType.Error, "Error in BeginPolling: " + ex.Message));
                            }
                            finally 
                            {
                                //cleanup after each connection
                                ConnectionClose();
                            }
                        }
                    }
                    Thread.Sleep(_PollingInterval);
                }
            }
            catch (Exception ex)
            {
                if (_Stop == false)
                {
                    _MessageLog.Add(new Message("Publisher Service", "localhost", EventLogEntryType.Error, "Unexpected error in outer try: " + ex.Message));
                }
            }
        }

        /// <summary>
        /// Method to construct a cube list, returned to clients via delegate
        /// </summary>
        /// <param name="concatenatedString"></param>
        private void PopulateCubeList(string concatenatedString)
        {
            string[] cubes = concatenatedString.Split(';');

            _CubeList = new List<CubeInfo>();

            foreach (string cube in cubes)
            {
                if (cube.Length > 0)
                {
                    CubeInfo cubeInfo = new CubeInfo();

                    string[] tempCubeArray = cube.Split(',');

                    cubeInfo.CubeHost = tempCubeArray[0].Trim();
                    cubeInfo.CubeDb = tempCubeArray[1].Trim();
                    cubeInfo.CubeName = tempCubeArray[2].Trim();

                    bool state;
                    if (tempCubeArray[3] == "READER")
                    {
                        state = true;
                    }
                    else
                    {
                        state = false;
                    }

                    cubeInfo.IsActive = state;
                    cubeInfo.GiveToUser = state;

                    try
                    {
                        cubeInfo.LastSchemaChange = Convert.ToDateTime(tempCubeArray[4].Trim());
                    }
                    catch 
                    {
                        //if it throws, set a default date of -60 and write error
                        _MessageLog.Add(new Message("Publisher Service", "localhost", EventLogEntryType.Error, MethodBase.GetCurrentMethod() + ": Last Schema Data not valid"));
                    }
                    _CubeList.Add(cubeInfo);
                }
            }
        }

        /// <summary>
        /// Helper method to convert returned date into string
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="concatenatedString"></param>
        /// <returns></returns>
        private string ConcatenateItem(OleDbDataReader reader, string concatenatedString)
        {
            while (reader.Read())
            {
                try
                {
                    int c = 0;

                    //using conditional operators to evaluation and values
                    //checks is field is null
                    //if null, then empty string, else, value

                    string serverName = reader.IsDBNull(c) ? string.Empty : reader.GetString(c);  //reader.GetString(c++) as string;

                    c = ++c;
                    string databaseName = reader.IsDBNull(c) ? string.Empty : reader.GetString(c);  //reader.GetString(c++) as string;

                    c = ++c;
                    string cubeName = reader.IsDBNull(c) ? string.Empty : reader.GetString(c);  //reader.GetString(c++) as string;

                    c = ++c;
                    string status = reader.IsDBNull(c) ? string.Empty : reader.GetString(c);  //reader.GetString(c++) as string;

                    c = ++c;
                    string lastSchemaUpdate = reader.IsDBNull(c) ? string.Empty : reader.GetString(c);  //reader.GetString(c++) as string;

                    concatenatedString = concatenatedString + serverName + "," + databaseName + "," + cubeName + "," + status + "," + lastSchemaUpdate + ";";
                }
                catch(Exception ex)
                { 
                    //if row has error
                    //reports to event viewer
                    if (_Stop == false)
                    {
                        _MessageLog.Add(new Message("Publisher Service", "localhost", EventLogEntryType.Error,  MethodBase.GetCurrentMethod() + ": " + ex.Message));
                    }
                }
            }

            return concatenatedString;
        }

        /// <summary>
        /// Opens connection to database
        /// </summary>
        private void Connect()
        {
            //modify to user either SQL Server or Oracle
            //if SQL Server connection does not contain these variables, no need to change
            //oracle requires values for userID and password
            //SQL server use integrated security, so can leave blank
            _ConnectionString = _ConnectionString.Replace("[DATASOURCE]", _DataSource);
            _ConnectionString = _ConnectionString.Replace("[USERID]", _UserID);
            _ConnectionString = _ConnectionString.Replace("[PASSWORD]", _Password);

            try
            {
                //checks ot make sure connection is closed and null
                //ConnectionClose();

                _Connection = new OleDbConnection(_ConnectionString);
                _Connection.Open();
            }
            catch (Exception ex)
            {
                _MessageLog.Add(new Message("Publisher Service", "localhost", EventLogEntryType.Error, MethodBase.GetCurrentMethod() + ": " + ex.Message));
            }
        }

        /// <summary>
        /// Closes connection to database
        /// </summary>        
        private void ConnectionClose()
        {
            if (_Connection != null)
            {
                if (_Connection.State == System.Data.ConnectionState.Open)
                {
                    _Connection.Close();
                }
                _Connection.Dispose();
                _Connection = null;
            }       
        }

        # endregion
    }
}
