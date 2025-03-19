using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PublisherServiceConsole
{
    class MainClass : IDisposable
    {
        private static Publisher.Publisher _Publisher = null;
        
        static void Main(string[] args)
        {
            string host = System.Configuration.ConfigurationManager.AppSettings["Host"] as string;
            string port = System.Configuration.ConfigurationManager.AppSettings["Port"] as string;
            
            _Publisher = new Publisher.Publisher(host, port);
        }
                
        #region dispose
        
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
                if (_Publisher != null)
                {
                    _Publisher.Dispose();
                }
            }
        }
        
        # endregion

    }
}
