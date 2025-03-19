using System.Collections.Generic;

namespace Publisher
{
    public class TrackedConnections
    {
        IDictionary<string, TrackedConnection> _Connections = new Dictionary<string, TrackedConnection>();
        ISubscriber _Client = null;

        public TrackedConnections(ISubscriber client)
        {
            _Client = client;
        }

        public void Add(string fullPath)
        {
            //constructs string from values
            string checkValue = string.Format("{0}", fullPath);

            //checks values against existing keys
            if (!_Connections.ContainsKey(checkValue))
            {
                //creates new item
                TrackedConnection newItem = new TrackedConnection();
                newItem.Key = checkValue;
                newItem.UserID = _Client.UserId;
                newItem.Workstation = _Client.Workstation;

                string firstCharacter = fullPath.ToLowerInvariant().Substring(0, 1);

                if (firstCharacter != "c" && firstCharacter != @"\")
                {
                    fullPath = GetUniversalName.GetUNC(@fullPath);
                }

                newItem.FullPath = fullPath;

                //adds items
                _Connections.Add(checkValue, newItem);

                //sends to server
                //that method marks if sent to server
                this.SendToServer(newItem);
            }
        }

        private void SendToServer(TrackedConnection item)
        {
            try
            {
                _Client.SendWorkbokForTracking(item);

                //marks as sent if it succeeds in sending
                item.SentToServer = true;
            }
            catch
            { 
                //message log
            }
        }
    }
}
