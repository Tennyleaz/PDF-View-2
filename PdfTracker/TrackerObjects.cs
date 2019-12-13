namespace PdfTracker
{
    public enum PDF_OP
    {
        Launch,
        LaunchDaily,
        AD,
        None
    }

    /// <summary>
    /// 當傳送完成或失敗會發生
    /// </summary>
    public class TrackerResult
    {
        public short StatusCode;
        public string Message;
        public TrackerExcptionType ExcptionType;
    }

    public enum TrackerExcptionType
    {
        /// <summary>
        /// 當 http request 成功執行 (但是不保證結果是 OK)
        /// </summary>
        Success = 0,
        /// <summary>
        /// constructor 或是 method 參數沒有填對
        /// </summary>
        ArgumentExcption,
        /// <summary>
        /// ignoreSSLWarning = false 時候無法驗證伺服器憑證
        /// </summary>
        SSLException,
        /// <summary>
        /// 其他 http 例外
        /// </summary>
        WebException,
        /// <summary>
        /// 通常是網路連線問題。Status code 會是 System.Net.Sockets.SocketError
        /// </summary>
        SocketException,
        /// <summary>
        /// 其他例外
        /// </summary>
        OtherException
    }
}
