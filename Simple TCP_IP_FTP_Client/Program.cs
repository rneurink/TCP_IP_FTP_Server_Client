using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simple_TCP_IP_FTP_Client
{
    /* Server return codes
     * 100 Series	The requested action is being initiated, expect another reply before proceeding with a new command.
     * 110	Restart marker replay . In this case, the text is exact and not left to the particular implementation; it must read: MARK yyyy = mmmm where yyyy is User-process data stream marker, and mmmm server's equivalent marker (note the spaces between markers and "=").
     * 120	Service ready in nnn minutes.
     * 125	Data connection already open; transfer starting.
     * 150	File status okay; about to open data connection.
     * 200 Series	The requested action has been successfully completed.
     * 202	Command not implemented, superfluous at this site.
     * 211	System status, or system help reply.
     * 212	Directory status.
     * 213	File status.
     * 214	Help message. Explains how to use the server or the meaning of a particular non-standard command. This reply is useful only to the human user.
     * 215	NAME system type. Where NAME is an official system name from the registry kept by IANA.
     * 220	Service ready for new user.
     * 221	Service closing control connection.
     * 225	Data connection open; no transfer in progress.
     * 226	Closing data connection. Requested file action successful (for example, file transfer or file abort).
     * 227	Entering Passive Mode (h1,h2,h3,h4,p1,p2).
     * 228	Entering Long Passive Mode (long address, port).
     * 229	Entering Extended Passive Mode (|||port|).
     * 230	User logged in, proceed. Logged out if appropriate.
     * 231	User logged out; service terminated.
     * 232	Logout command noted, will complete when transfer done.
     * 234	Specifies that the server accepts the authentication mechanism specified by the client, and the exchange of security data is complete. A higher level nonstandard code created by Microsoft.
     * 250	Requested file action okay, completed.
     * 257	"PATHNAME" created.
     * 300 Series	The command has been accepted, but the requested action is on hold, pending receipt of further information.
     * 331	User name okay, need password.
     * 332	Need account for login.
     * 350	Requested file action pending further information
     * 400 Series	The command was not accepted and the requested action did not take place, but the error condition is temporary and the action may be requested again.
     * 421	Service not available, closing control connection. This may be a reply to any command if the service knows it must shut down.
     * 425	Can't open data connection.
     * 426	Connection closed; transfer aborted.
     * 430	Invalid username or password
     * 434	Requested host unavailable.
     * 450	Requested file action not taken.
     * 451	Requested action aborted. Local error in processing.
     * 452	Requested action not taken. Insufficient storage space in system.File unavailable (e.g., file busy).
     * 500 Series	Syntax error, command unrecognized and the requested action did not take place. This may include errors such as command line too long.
     * 501	Syntax error in parameters or arguments.
     * 502	Command not implemented.
     * 503	Bad sequence of commands.
     * 504	Command not implemented for that parameter.
     * 530	Not logged in.
     * 532	Need account for storing files.
     * 534	Could Not Connect to Server - Policy Requires SSL
     * 550	Requested action not taken. File unavailable (e.g., file not found, no access).
     * 551	Requested action aborted. Page type unknown.
     * 552	Requested file action aborted. Exceeded storage allocation (for current directory or dataset).
     * 553	Requested action not taken. File name not allowed.
     * 600 Series	Replies regarding confidentiality and integrity
     * 631	Integrity protected reply.
     * 632	Confidentiality and integrity protected reply.
     * 633	Confidentiality protected reply.
     * 10000  Series	Common Winsock Error Codes
     * 10054	Connection reset by peer. The connection was forcibly closed by the remote host.
     * 10060	Cannot connect to remote server.
     * 10061	Cannot connect to remote server. The connection is actively refused by the server.
     * 10066	Directory not empty.
     * 10068	Too many users, server is full.
     */

    class Program
    {
        private static Socket _ClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private static bool _DataTransferEnabled;
        private static TcpClient _DataListener;
        private static int _ComPort = 21;
        private static byte[] _receiveBuffer = new byte[1024];

        static void Main(string[] args)
        {
            Console.Title = "FTP Client";
            Console.Write("Enter ip addres: ");
            string ip = Console.ReadLine();
            Console.Write("Enter username: ");
            string user = Console.ReadLine();
            Console.Write("Enter password: ");
            string password = Console.ReadLine();
            LoopConnect(ip, user, password);
            LoopCommands();
        }

        private static void LoopCommands()
        {
            while (_ClientSocket.Connected)
            {
                Console.WriteLine("Enter a command: ");
                string command = Console.ReadLine();

                if (command.ToLower().StartsWith("exit") || command.ToLower().StartsWith("quit"))
                {
                    string response = SendCommand("QUIT");
                    if (response.StartsWith("221"))
                    {
                        _ClientSocket.Shutdown(SocketShutdown.Both);
                        _ClientSocket.Close();
                        Environment.Exit(0);
                    }
                }
                else SendCommand(command);
            }
        }

        private static void LoopConnect(string ip, string username, string password)
        {
            int connectionAttempts = 0;
            while (!_ClientSocket.Connected)
            {
                try
                {
                    connectionAttempts++;
                    _ClientSocket.Connect(ip, _ComPort);
                }
                catch (SocketException)
                {
                    Console.Clear();
                    Console.WriteLine($@"Connection failed, attempts:{connectionAttempts}");
                }
            }
            Console.Clear();
            Console.WriteLine("Connected");
            string response = string.Empty;
            response = AwaitResponse();
            if (!response.StartsWith("220"))
            {
                Console.WriteLine("Server is not accepting clients");
                _ClientSocket.Shutdown(SocketShutdown.Both);
                _ClientSocket.Close();
                return;
            }
            response = SendCommand($@"USER {username}");
            if (response.StartsWith("331")) { SendCommand($@"PASS {password}"); }
        }

        private static string SendCommand(string command)
        {
            string response = null;
            try
            {
                Console.WriteLine("Sending: " + command);
                command += "\r\n";
                _ClientSocket.Send(Encoding.ASCII.GetBytes(command));
                response = AwaitResponse();
            }
            catch (SocketException ex)
            { Console.WriteLine("SendCommand " + ex.Message); }
            catch (ObjectDisposedException ex)
            { Console.WriteLine("SendCommand " + ex.Message); }
            return response;
        }

        private static string AwaitResponse()
        {
            string response = null;
            try
            {
                int received = _ClientSocket.Receive(_receiveBuffer);
                byte[] receiveddata = new byte[received];
                Array.Copy(_receiveBuffer, receiveddata, received);
                response = Encoding.ASCII.GetString(receiveddata);
                Console.WriteLine($@"Received: {Encoding.ASCII.GetString(receiveddata)}");
            }
            catch (SocketException ex)
            { Console.WriteLine("AwaitResponse " + ex.Message); }
            catch (ObjectDisposedException ex)
            { Console.WriteLine("AwaitResponse " + ex.Message); }
            return response;
        }
    }
}
