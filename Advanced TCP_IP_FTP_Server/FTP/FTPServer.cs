using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;

namespace Advanced_TCP_IP_FTP_Server
{
    class FTPServer
    {
        public ArrayList FTPClientsList = new ArrayList();
        private static TcpListener _FTPListener;

        /// <summary>
        /// Starts the ftp server
        /// </summary>
        /// <returns></returns>
        public bool StartServer()
        {
            try
            {
                StopServer();
                FTP.NewServerLog("Setting up server...");
                _FTPListener = new TcpListener(IPAddress.Any, FTP.FTPPort);
                _FTPListener.Start(5);

                _FTPListener.BeginAcceptSocket(new AsyncCallback(AcceptCallback), _FTPListener);
                FTP.NewServerLog("Server started");
                return true;
            }
            catch (Exception ex) { FTP.NewServerLog($@"Error starting server {ex.ToString()}"); }
            return false;
        }

        /// <summary>
        /// Stops the ftp server
        /// </summary>
        public void StopServer()
        {
            if (_FTPListener != null) _FTPListener.Stop(); _FTPListener = null;
            if (FTPClientsList.Count > 0)
            {
                foreach (FTPClient client in FTPClientsList)
                {
                    client.ClientSocket.Disconnect(false);
                }
            }
            FTP.NewServerLog("Server stopped");
        }

        /// <summary>
        /// Callback for when a new client connects
        /// </summary>
        /// <param name="ar"></param>
        private void AcceptCallback(IAsyncResult ar)
        {
            try { FTPClientsList.Add(new FTPClient(_FTPListener.EndAcceptSocket(ar))); }
            catch (Exception ex) { FTP.NewServerLog($@"Error adding client to list {ex.ToString()}"); }
            FTP.NewServerLog("New client connected");
            try { _FTPListener.BeginAcceptSocket(new AsyncCallback(AcceptCallback), _FTPListener); }
            catch (Exception ex) { FTP.NewServerLog($@"Error starting AcceptCallback {ex.ToString()}"); }
        }
    }
}
