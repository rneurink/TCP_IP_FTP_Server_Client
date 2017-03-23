using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;

namespace Simple_TCP_IP_FTP_Server
{
    class Program
    {
        public static ArrayList FTPClientsList = new ArrayList();
        public static Dictionary<string, string> UserList = new Dictionary<string, string>();
        public static Dictionary<string, string> StartingDirList = new Dictionary<string, string>();

        private static TcpListener _FTPListener; 

        static void Main(string[] args)
        {
            Console.Title = "FTP Server";
            UserList.Add("Admin", "Admin");
            StartingDirList.Add("Admin", "D:\\Downloads");
            SetupServer();
            Console.ReadLine();
        }

        private static void SetupServer()
        {
            Console.WriteLine("Setting up server...");
            _FTPListener = new TcpListener(IPAddress.Loopback,21);
            _FTPListener.Start(5);

            _FTPListener.BeginAcceptSocket(new AsyncCallback(AcceptCallback), _FTPListener);
            Console.WriteLine("Server started");
        }

        private static void AcceptCallback(IAsyncResult ar)
        {
            FTPClientsList.Add(new FTPClient(_FTPListener.EndAcceptSocket(ar)));
            Console.WriteLine("New client connected");
            _FTPListener.BeginAcceptSocket(new AsyncCallback(AcceptCallback), _FTPListener);
        }
    }
}
