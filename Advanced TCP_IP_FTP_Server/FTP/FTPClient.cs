using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Threading;
using System.Collections;

namespace Advanced_TCP_IP_FTP_Server
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

    class FTPClient
    {
        //This socket handles the commands (port 21 default)
        public Socket ClientSocket;
        public IPEndPoint ClientEndPoint;
        public DateTime ConnectedAt;

        private static byte[] _Data = new byte[1024];
        private string _RenameFP = "";

        private bool _DataTransferEnabled = false;
        //This listener handles the data transfers
        private TcpListener _DataListener = null;
        private FTPUser _FTPUser;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="clientsocket"></param>
        public FTPClient(Socket clientsocket)
        {
            try
            {
                this.ClientSocket = clientsocket;
                ConnectedAt = DateTime.Now;
                ClientSocket.NoDelay = false;

                _FTPUser = new FTPUser();

                SendMessage("220 FTP Ready\r\n");

                ClientSocket.BeginReceive(_Data, 0, _Data.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), ClientSocket);
            }
            catch (SocketException ex)
            { FTP.NewServerLog("BeginReceive " + ex.Message); }
            catch (ObjectDisposedException ex)
            { FTP.NewServerLog("BeginReceive " + ex.Message); }

        }

        /// <summary>
        /// Callback for when something is received from the client. Also handles commands
        /// </summary>
        /// <param name="ar"></param>
        private void ReceiveCallback(IAsyncResult ar)
        {
            //TODO add socketexception and objectdisposedexception

            try
            {
                //Returns how many bytes are received
                int received = ClientSocket.EndReceive(ar);
                if (received == 0) { Disconnect(); return; }

                //Trim the buffer and restart the receive callback
                byte[] receivedBuffer = new byte[received];
                Array.Copy(_Data, receivedBuffer, received);
                try { ClientSocket.BeginReceive(_Data, 0, _Data.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), ClientSocket); }
                catch { Disconnect(); }


                //Seperate and trim the command
                string CmdText = Encoding.ASCII.GetString(receivedBuffer, 0, received).TrimStart(' ');
                string CmdArguments = null, Command = null;
                int EndIndex = 0;
                if ((EndIndex = CmdText.IndexOf(' ')) == -1) EndIndex = (CmdText = CmdText.Trim()).Length;
                else CmdArguments = CmdText.Substring(EndIndex).TrimStart(' ');
                Command = CmdText.Substring(0, EndIndex).ToUpper();
                bool CmdExecuted = false;

                if (CmdArguments != null && CmdArguments.EndsWith("\r\n")) CmdArguments = CmdArguments.Substring(0, CmdArguments.Length - 2);

                switch (Command)
                {
                    //Authentication username
                    case "USER":
                        if (CmdArguments != null && CmdArguments.Length > 0)
                        {
                            _FTPUser.LoadUser(CmdArguments);
                            SendMessage("331 Password required!\r\n");
                        }
                        CmdExecuted = true;
                        break;
                    //Authentication password
                    case "PASS":
                        if (!_FTPUser.IsEnabled) { SendMessage("530 Authentication Failed!\r\n"); return; }
                        if (_FTPUser.Username == "" || _FTPUser.Username == null) { SendMessage("503 Invalid User Name\r\n"); return; }

                        if (_FTPUser.Authenticate(CmdArguments)) { SendMessage("230 Authentication Successful\r\n"); FTP.NewServerLog($@"User {_FTPUser.Username} logged in from {ClientSocket.RemoteEndPoint.ToString()}"); }
                        else SendMessage("530 Authentication Failed!\r\n");
                        CmdExecuted = true;
                        break;
                }
                if (!CmdExecuted)
                {
                    if (_FTPUser.IsAuthenticated)
                    {
                        //All available commands when authenticated
                        switch (Command)
                        {
                            case "CDUP": CDUP(CmdArguments); break;
                            //Change working directory.
                            case "CWD":
                                string dir = GetExactPath(CmdArguments);

                                if (_FTPUser.ChangeWorkingDir(dir)) SendMessage("250 CWD command successful.\r\n");
                                else SendMessage("550 System can't find directory '" + dir + "'.\r\n");
                                break;

                            case "DELE": DELE(CmdArguments); break;
                            //TODO case "FEAT": FEAT(CmdArguments); break;
                            case "LIST": LIST(CmdArguments); break;
                            case "MKD": MKD(CmdArguments); break;
                            case "NLST": NLST(CmdArguments); break;
                            //No operation (dummy packet; used mostly on keepalives).
                            case "NOOP": SendMessage("200 OK\r\n"); break;
                            case "PASV": PASV(CmdArguments); break;
                            case "PORT": PORT(CmdArguments); break;
                            //Print working directory. Returns the current directory of the host.
                            case "PWD": SendMessage("257 \"" + _FTPUser.WorkingDir.Replace('\\', '/') + "\"\r\n"); break;
                            //Disconnect.
                            case "QUIT": SendMessage("221 FTP server signing off\r\n"); Disconnect(); break;
                            case "RETR": RETR(CmdArguments); break;
                            case "RMD": RMD(CmdArguments); break;
                            case "RNFR": RNFR(CmdArguments); break;
                            case "RNTO": RNTO(CmdArguments); break;
                            case "STOR": STOR(CmdArguments); break;
                            //Return system type.
                            case "SYST": SendMessage("215 Windows_NT\r\n"); break;
                            case "TYPE": TYPE(CmdArguments); break;
                            default: SendMessage("500 Unknown Command.\r\n"); break;
                        }
                    }
                    else SendMessage("530 Access Denied! Authenticate first\r\n");
                }
            }
            catch (SocketException ex)
            { FTP.NewServerLog("ReceiveCallback " + ex.Message); }
            catch (ObjectDisposedException ex)
            { FTP.NewServerLog("ReceiveCallback " + ex.Message); }
            catch (NullReferenceException ex)
            { FTP.NewServerLog("ReceiveCallback " + ex.Message); }
        }

        #region Command Handlers
        /// <summary>
        /// Change to Parent Directory.
        /// </summary>
        /// <param name="CmdArguments"></param>
        private void CDUP(string CmdArguments)
        {
            string[] _Pathparts = _FTPUser.WorkingDir.Split('\\');
            if (_Pathparts.Length > 1)
            {
                _FTPUser.WorkingDir = "";
                for (int i = 0; i < (_Pathparts.Length - 2); i++)
                {
                    _FTPUser.WorkingDir += _Pathparts[i] + "\\";
                }
            }
            SendMessage("250 CDUP command successful.\r\n");
        }

        /// <summary>
        /// Delete file.
        /// </summary>
        /// <param name="CmdArguments"></param>
        private void DELE(string CmdArguments)
        {
            string Path = GetExactPath(CmdArguments);
            Path = _FTPUser.RootDir + Path.Substring(0, Path.Length - 1);
            try
            {
                if (File.Exists(Path))
                {
                    if (_FTPUser.CanDeleteFiles)
                    {
                        FileInfo FI = new FileInfo(Path);
                        FI.Attributes = FileAttributes.Normal; // This is required to delete a readonly file
                        File.Delete(Path);
                        SendMessage("250 File deleted.\r\n");
                        FTP.NewServerLog($@"User {_FTPUser.Username} deleted {Path}");
                    }
                    else SendMessage("550 Access Denied.\r\n");
                }
                else SendMessage("550 File dose not exist.\r\n");
            }
            catch (Exception Ex) { SendMessage("550 " + Ex.Message + ".\r\n"); }
        }

        /// <summary>
        /// Lists all available commands
        /// </summary>
        /// <param name="CmdArguments"></param>
        private void FEAT(string CmdArguments)
        {
            //TODO implement
        }

        /// <summary>
        /// Returns information of a file or directory if specified, else information of the current working directory is returned.
        /// </summary>
        /// <param name="CmdArguments"></param>
        private void LIST(string CmdArguments)
        {
            string Path = _FTPUser.RootDir + GetExactPath(CmdArguments);
            if (!_FTPUser.CanViewHiddenFiles && (new DirectoryInfo(Path).Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
            {
                SendMessage("550 Invalid path specified.\r\n");
                return;
            }

            Socket DataSocket = GetDataSocket();
            if (DataSocket == null)
            {
                return;
            }

            try
            {
                string[] FilesList = Directory.GetFiles(Path, "*.*", SearchOption.TopDirectoryOnly);
                string[] FoldersList = Directory.GetDirectories(Path, "*.*", SearchOption.TopDirectoryOnly);
                string strFilesList = "";

                if (_FTPUser.CanViewHiddenFolders)
                {
                    foreach (string Folder in FoldersList)
                    {
                        string date = Directory.GetCreationTime(Folder).ToString("MM-dd-yy hh:mmtt");
                        strFilesList += date + " <DIR> " + Folder.Substring(Folder.Replace('\\', '/').LastIndexOf('/') + 1) + "\r\n";
                    }
                }
                else
                {
                    foreach (string Folder in FoldersList)
                    {
                        if ((new DirectoryInfo(Folder).Attributes & FileAttributes.Hidden) == FileAttributes.Hidden) continue;

                        string date = Directory.GetCreationTime(Folder).ToString("MM-dd-yy hh:mmtt");
                        strFilesList += date + " <DIR> " + Folder.Substring(Folder.Replace('\\', '/').LastIndexOf('/') + 1) + "\r\n";
                    }
                }

                if (_FTPUser.CanViewHiddenFiles)
                {
                    foreach (string FileName in FilesList)
                    {
                        string date = File.GetCreationTime(FileName).ToString("MM-dd-yy hh:mmtt");
                        strFilesList += date + " " + new FileInfo(FileName).Length.ToString() + " " + FileName.Substring(FileName.Replace('\\', '/').LastIndexOf('/') + 1) + "\r\n";
                    }
                }
                else
                {
                    foreach (string FileName in FilesList)
                    {
                        if ((File.GetAttributes(FileName) & FileAttributes.Hidden) == FileAttributes.Hidden) continue;

                        string date = File.GetCreationTime(FileName).ToString("MM-dd-yy hh:mmtt");
                        strFilesList += date + " " + new FileInfo(FileName).Length.ToString() + " " + FileName.Substring(FileName.Replace('\\', '/').LastIndexOf('/') + 1) + "\r\n";
                    }
                }
                DataSocket.Send(System.Text.Encoding.Default.GetBytes(strFilesList));
                SendMessage("226 Transfer Complete.\r\n");
            }
            catch (DirectoryNotFoundException)
            {
                SendMessage("550 Invalid path specified.\r\n");
            }
            catch
            {
                SendMessage("426 Connection closed; transfer aborted.\r\n");
            }
            finally
            {
                DataSocket.Shutdown(SocketShutdown.Both);
                DataSocket.Close(); DataSocket = null;
            }
        }

        /// <summary>
        /// Make directory.
        /// </summary>
        /// <param name="CmdArguments"></param>
        private void MKD(string CmdArguments)
        {
            if (!_FTPUser.CanStoreFolders)
            {
                SendMessage("550 Access Denied.\r\n");
                return;
            }

            string Path = _FTPUser.RootDir + GetExactPath(CmdArguments);

            if (Directory.Exists(Path) || File.Exists(Path))
                SendMessage("550 A file or folder with the same name already exists.\r\n");
            else
            {
                try
                {
                    Directory.CreateDirectory(Path);
                    SendMessage("257 \"" + Path + "\" directory created.\r\n");
                    FTP.NewServerLog($@"User {_FTPUser.Username} created {Path}");
                }
                catch (Exception Ex) { SendMessage("550 " + Ex.Message + ".\r\n"); }
            }
        }

        /// <summary>
        /// Returns a list of file names in a specified directory.
        /// </summary>
        /// <param name="CmdArguments"></param>
        private void NLST(string CmdArguments)
        {
            string Path = _FTPUser.RootDir + GetExactPath(CmdArguments);
            if (!Directory.Exists(Path))
            {
                SendMessage("550 Invalid Path.\r\n");
                return;
            }

            Socket DataSocket = GetDataSocket();
            if (DataSocket == null)
            {
                return;
            }

            try
            {
                string[] FoldersList = Directory.GetDirectories(Path, "*.*", SearchOption.TopDirectoryOnly);
                string FolderList = "";
                foreach (string Folder in FoldersList)
                {
                    FolderList += Folder.Substring(Folder.Replace('\\', '/').LastIndexOf('/') + 1) + "\r\n";
                }
                DataSocket.Send(System.Text.Encoding.Default.GetBytes(FolderList));
                DataSocket.Shutdown(SocketShutdown.Both);
                DataSocket.Close();

                SendMessage("226 Transfer Complete.\r\n");
            }
            catch
            {
                SendMessage("426 Connection closed; transfer aborted.\r\n");
            }
        }

        /// <summary>
        /// Enter passive mode.
        /// </summary>
        /// <param name="CmdArguments"></param>
        private void PASV(string CmdArguments)
        {
            int TempPort = FTP.MinPasvPort;
            LoopListener:
            if (_DataListener != null) { _DataListener.Stop(); _DataListener = null; }
            try
            {
                _DataListener = new TcpListener(IPAddress.Any, TempPort);
                _DataListener.Start();
            }
            catch
            {
                if (TempPort < FTP.MaxPasvPort)
                {
                    TempPort++;
                    goto LoopListener;
                }
                else
                {
                    SendMessage("500 Action Failed Retry\r\n");
                    return;
                }
            }
            string SocketEndPoint = ClientSocket.LocalEndPoint.ToString();
            SocketEndPoint = SocketEndPoint.Substring(0, SocketEndPoint.IndexOf(":")).Replace(".", ",") + "," + (TempPort >> 8) + "," + (TempPort & 255);
            _DataTransferEnabled = true;

            SendMessage("227 Entering Passive Mode (" + SocketEndPoint + ").\r\n");
        }

        /// <summary>
        /// Specifies an address and port to which the server should connect.
        /// </summary>
        /// <param name="CmdArguments"></param>
        private void PORT(string CmdArguments)
        {
            string[] IP_Components = CmdArguments.Split(',');
            if (IP_Components.Length != 6)
            {
                SendMessage("550 Invalid arguments.\r\n");
                return;
            }

            string ClientIP = IP_Components[0] + "." + IP_Components[1] + "." + IP_Components[2] + "." + IP_Components[3];
            int TempPort = (Convert.ToInt32(IP_Components[4]) << 8) | Convert.ToInt32(IP_Components[5]);
            ClientEndPoint = new IPEndPoint(Dns.GetHostEntry(ClientIP).AddressList[0], TempPort);
            _DataTransferEnabled = false;
            SendMessage("200 Ready to connect to " + ClientIP + "\r\n");
        }

        /// <summary>
        /// Retrieve a copy of the file
        /// </summary>
        /// <param name="CmdArguments"></param>
        private void RETR(string CmdArguments)
        {
            if (!_FTPUser.CanCopyFiles)
            {
                SendMessage("426 Access Denied.\r\n");
                return;
            }

            string ReturnMessage = string.Empty;

            FileStream FS = null;
            Socket DataSocket = null;
            try
            {
                string Path = _FTPUser.RootDir + GetExactPath(CmdArguments);
                Path = Path.Substring(0, Path.Length - 1);

                if (!_FTPUser.CanViewHiddenFiles && (File.GetAttributes(Path) & FileAttributes.Hidden) == FileAttributes.Hidden)
                {
                    SendMessage("550 Access Denied or invalid path.\r\n");
                    return;
                }

                FS = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch
            {
                ReturnMessage = "550 Access denied or invalid path!\r\n";
                goto FinaliseAll;
            }


            DataSocket = GetDataSocket();
            if (DataSocket == null)
                goto FinaliseAll;

            try
            {
                byte[] data = new byte[(FS.Length > 100000) ? 100000 : (int)FS.Length];
                while (DataSocket.Send(data, 0, FS.Read(data, 0, data.Length), SocketFlags.None) != 0) ;
                ReturnMessage = "226 Transfer Complete.\r\n";
                FTP.NewServerLog($@"User {_FTPUser.Username} downloaded {CmdArguments}");
            }
            catch
            {
                ReturnMessage = "426 Transfer aborted.\r\n";
            }

            FinaliseAll:
            if (FS != null) FS.Close(); FS = null;
            if (DataSocket != null && DataSocket.Connected)
            {
                DataSocket.Shutdown(SocketShutdown.Both);
                DataSocket.Close();
            }
            DataSocket = null;
            SendMessage(ReturnMessage);
        }

        /// <summary>
        /// Remove a directory.
        /// </summary>
        /// <param name="CmdArguments"></param>
        private void RMD(string CmdArguments)
        {
            if (!_FTPUser.CanDeleteFolders)
            {
                SendMessage("550 Access Denied.\r\n");
                return;
            }

            string Path = _FTPUser.RootDir + GetExactPath(CmdArguments);

            if (Directory.Exists(Path))
            {
                try
                {
                    Directory.Delete(Path, true);
                    SendMessage("250 \"" + Path + "\" deleted.\r\n");
                    FTP.NewServerLog($@"User {_FTPUser.Username} deleted {Path}");
                }
                catch (Exception Ex) { SendMessage("550 " + Ex.Message + ".\r\n"); }
            }
            else SendMessage("550 Folder dose not exist.\r\n");
        }

        /// <summary>
        /// Rename from.
        /// </summary>
        /// <param name="CmdArguments"></param>
        private void RNFR(string CmdArguments)
        {
            if (!_FTPUser.CanRenameFiles) { SendMessage("550 Access Denied.\r\n"); return; }

            string Path = _FTPUser.RootDir + GetExactPath(CmdArguments);
            string Filepath = Path;
            if (Path.EndsWith("\\")) { Filepath = Filepath.Substring(0, Filepath.Length - 1); }
            if (Directory.Exists(Path) || File.Exists(Filepath))
            {
                _RenameFP = Path;
                SendMessage("350 Please specify destination name.\r\n");
            }
            else SendMessage("550 File or directory doesn't exist.\r\n");
        }

        /// <summary>
        /// Rename to.
        /// </summary>
        /// <param name="CmdArguments"></param>
        private void RNTO(string CmdArguments)
        {
            if (_RenameFP.Length == 0)
            {
                SendMessage("503 Bad sequence of commands.\r\n");
                return;
            }

            string Path = _FTPUser.RootDir + GetExactPath(CmdArguments);
            string Filepath = Path;
            if (Path.EndsWith("\\")) { Filepath = Filepath.Substring(0, Filepath.Length - 1); }

            string RenameFile = _RenameFP;
            if (_RenameFP.EndsWith("\\")) { RenameFile = RenameFile.Substring(0, RenameFile.Length - 1); }
            if (Directory.Exists(Path) || File.Exists(Filepath))
                SendMessage("550 File or folder with the same name already exists.\r\n");
            else
            {
                try
                {
                    if (Directory.Exists(_RenameFP))
                    {
                        if (_FTPUser.CanRenameFolders) { Directory.Move(_RenameFP, Path); SendMessage("250 Folder renamed successfully.\r\n"); FTP.NewServerLog($@"User {_FTPUser.Username} renamed {_RenameFP} to {CmdArguments}"); }
                        else SendMessage("550 Access Denied.\r\n");
                    }
                    else if (File.Exists(RenameFile))
                    {
                        if (_FTPUser.CanRenameFiles) { File.Move(RenameFile, Path); SendMessage("250 File renamed successfully.\r\n"); FTP.NewServerLog($@"User {_FTPUser.Username} renamed {_RenameFP} to {CmdArguments}"); }
                        else SendMessage("550 Access Denied.\r\n");
                    }
                    else SendMessage("550 Source file dose not exists.\r\n");
                }
                catch (Exception Ex) { SendMessage("550 " + Ex.Message + ".\r\n"); }
            }
            _RenameFP = "";
        }

        /// <summary>
        /// Accept the data and to store the data as a file at the server site
        /// </summary>
        /// <param name="CmdArguments"></param>
        private void STOR(string CmdArguments)
        {
            if (!_FTPUser.CanStoreFiles)
            {
                SendMessage("426 Access Denied.\r\n");
                return;
            }
            Stream FS = null;

            string Path = _FTPUser.RootDir + GetExactPath(CmdArguments);
            Path = Path.Substring(0, Path.Length - 1);

            try
            {
                FS = new FileStream(Path, FileMode.Create, FileAccess.Write, FileShare.None);
            }
            catch (Exception Ex)
            {
                SendMessage("550 " + Ex.Message + "\r\n");
                return;
            }

            Socket DataSocket = GetDataSocket();
            if (DataSocket == null)
            {
                return;
            }
            try
            {
                int ReadBytes = 1;
                byte[] tmpBuffer = new byte[10000];

                do
                {
                    ReadBytes = DataSocket.Receive(tmpBuffer);
                    FS.Write(tmpBuffer, 0, ReadBytes);
                } while (ReadBytes > 0);

                tmpBuffer = null;

                SendMessage("226 Transfer Complete.\r\n");
                FTP.NewServerLog($@"User {_FTPUser.Username} uploaded {CmdArguments}");
            }
            catch
            {
                SendMessage("426 Connection closed unexpectedly.\r\n");
            }
            finally
            {
                if (DataSocket != null)
                {
                    DataSocket.Shutdown(SocketShutdown.Both);
                    DataSocket.Close();
                    DataSocket = null;
                }
                FS.Close(); FS = null;
            }
        }

        /// <summary>
        /// Sets the transfer mode (ASCII/Binary).
        /// </summary>
        /// <param name="CmdArguments"></param>
        private void TYPE(string CmdArguments)
        {
            if ((CmdArguments = CmdArguments.Trim().ToUpper()) == "A" || CmdArguments == "I")
                SendMessage("200 Type " + CmdArguments + " Accepted.\r\n");
            else SendMessage("500 Unknown Type.\r\n");
        }
        #endregion

        #region Generic Functions
        /// <summary>
        /// Disconnects the client
        /// </summary>
        private void Disconnect()
        {
            try
            {
                if (ClientSocket.Connected) ClientSocket.Shutdown(SocketShutdown.Both);
                if (ClientSocket != null && ClientSocket.Connected) ClientSocket.Close(); ClientSocket = null;
                if (_DataListener != null) _DataListener.Stop(); _DataListener = null;
                ClientEndPoint = null;
                _FTPUser = null;
                FTP.FtpServer.FTPClientsList.Remove(this);
                FTP.NewServerLog("Client disconnected");
                GC.Collect();
            }
            catch (SocketException ex)
            { FTP.NewServerLog("Disconnect " + ex.Message); }
            catch (ObjectDisposedException ex)
            { FTP.NewServerLog("Disconnect " + ex.Message); }


        }

        /// <summary>
        /// Sends a message to the client
        /// </summary>
        /// <param name="data"></param>
        private void SendMessage(string data)
        {
            try { ClientSocket.Send(Encoding.ASCII.GetBytes(data)); }
            catch { Disconnect(); }
        }

        /// <summary>
        /// Gets the exact remote path of the files
        /// </summary>
        /// <param name="Path"></param>
        /// <returns></returns>
        string GetExactPath(string Path)
        {
            if (Path == null) Path = "";

            string _Dir = Path.Replace("/", "\\");

            if (!_Dir.EndsWith("\\")) _Dir += "\\";

            if (!Path.StartsWith("/")) _Dir = _FTPUser.WorkingDir + _Dir;

            ArrayList Path_Parts = new ArrayList();
            _Dir = _Dir.Replace("\\\\", "\\");
            string[] p = _Dir.Split('\\');
            Path_Parts.AddRange(p);

            for (int i = 0; i < Path_Parts.Count; i++)
            {
                if (Path_Parts[i].ToString() == "..")
                {
                    if (i > 0)
                    {
                        Path_Parts.RemoveAt(i - 1);
                        i--;
                    }

                    Path_Parts.RemoveAt(i);
                    i--;
                }
            }

            return _Dir.Replace("\\\\", "\\");
        }

        /// <summary>
        /// Gets the datasocket through wich files are being send
        /// </summary>
        /// <returns></returns>
        Socket GetDataSocket()
        {
            Socket DataSocket = null;
            try
            {
                if (_DataTransferEnabled)
                {
                    int Count = 0;
                    while (!_DataListener.Pending())
                    {
                        Thread.Sleep(1000);
                        Count++;
                        // Time out after 30 seconds
                        if (Count > 29)
                        {
                            SendMessage("425 Data Connection Timed out\r\n");
                            return null;
                        }
                    }

                    DataSocket = _DataListener.AcceptSocket();
                    SendMessage("125 Connected, Starting Data Transfer.\r\n");
                }
                else
                {
                    SendMessage("150 Connecting.\r\n");
                    DataSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    DataSocket.Connect(ClientEndPoint);
                }
            }
            catch
            {
                SendMessage("425 Can't open data connection.\r\n");
                return null;
            }
            finally
            {
                if (_DataListener != null)
                {
                    _DataListener.Stop();
                    _DataListener = null;
                    GC.Collect();
                }
            }

            _DataTransferEnabled = false;

            return DataSocket;
        }
        #endregion
    }
}
