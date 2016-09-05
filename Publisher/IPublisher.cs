using System.Collections.Generic;
using System.Data;
using System.ServiceModel;

namespace Publisher
{
    [ServiceContract(SessionMode = SessionMode.Required, CallbackContract = typeof(ISubscriber))]
    public interface IPublisher
    { 
        [OperationContract(IsOneWay = false, IsInitiating = true)]
        void Attach(string userId, string workstation, string application);
        
        [OperationContract(IsOneWay = false, IsTerminating = true)]
        void Detach();

        [OperationContract(IsOneWay = true)]
        void Notify();

        [OperationContract(IsOneWay = false)]
        IList<CubeInfo> GetAvailableCubeStatus();

        [OperationContract(IsOneWay = false)]
        CubeInfo GetActiveCube();

        [OperationContract(IsOneWay = false)]
        DataSet GetLoadStatusByDate(string viewDate);

        [OperationContract(IsOneWay = false)]
        DataSet GetLoadStatusBySource(string viewDate);

        [OperationContract(IsOneWay = false)]
        DataSet GetLoadStatusByRegion(string viewDate);

        [OperationContract(IsOneWay = false)]
        void SendWorkbokForTracking(TrackedConnection item);
    }
    
    public interface ISubscriber
    {
        string UserId { get; set; }
        string Workstation { get; set; }
        string Title { get; set; }
        string Host { get; set; }
        string Port { get; set; }

        bool IsConnected { get; }

        TrackedConnections Tracker { get; set; }

        [OperationContract(IsOneWay = false)]
        void Attach(string userId, string workstation, string application);

        # region Methods used for Excel clients, used with delegate for status updates

        [OperationContract(IsOneWay = true)]
        void Update(IList<CubeInfo> message);

        [OperationContract(IsOneWay = false)]
        void Run();

        [OperationContract(IsOneWay = false)]
        void SendWorkbokForTracking(TrackedConnection item);

        # endregion
        
        # region Methods for legacy clients - single execution with return

        [OperationContract(IsOneWay = false)]
        IList<CubeInfo> GetAvailableCubeStatus();

        [OperationContract(IsOneWay = false)]
        CubeInfo GetActiveCube();

        # endregion
        
        # region Load Status methods - returns slices to load information

        [OperationContract(IsOneWay = false)]
        DataSet GetLoadStatusByDate(string viewDate);

        [OperationContract(IsOneWay = false)]
        DataSet GetLoadStatusBySource(string viewDatem);

        [OperationContract(IsOneWay = false)]
        DataSet GetLoadStatusByRegion(string viewDate);

        # endregion
    }
}