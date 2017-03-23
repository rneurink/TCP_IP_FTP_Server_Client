using System;

namespace Advanced_TCP_IP_FTP_Server
{
    public delegate void ServerLogHandler(object sender, ServerLogArgs args);

    public class ServerLogArgs : EventArgs
    {
        public string Message { get; set; }
    }
}
