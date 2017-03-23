using System;

namespace Advanced_TCP_IP_FTP_Client
{
    public delegate void ClientLogHandler(object sender, ClientLogArgs args);

    public class ClientLogArgs : EventArgs
    {
        public string Message { get; set; }
    }
}
