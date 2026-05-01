namespace OSSMWebServer
{
    public enum ECommand : int
    {
        UnknownError,

        #region Host
        CreateRoom,
        JoinRequest,
        #endregion

        #region Guest
        JoinRoom,
        #endregion

        #region Both
        LeaveRoom,
        #endregion
    }
}
