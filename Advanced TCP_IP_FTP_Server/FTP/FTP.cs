using System;
using System.IO;
using System.Text;
using System.Xml;

namespace Advanced_TCP_IP_FTP_Server
{
    public enum SettingsKey
    {
        MAX_PASV_PORT,
        MIN_PASV_PORT,
        FTP_PORT
    }

    //Main class, also handles the xml reading and writing
    class FTP
    {
        public static event ServerLogHandler ServerLogEvent;

        public static FTPServer FtpServer;
        public static int MaxPasvPort
        {
            get
            {
                return Convert.ToInt32(GetSettingsAsString(SettingsKey.MAX_PASV_PORT));
            }
            set
            {
                ChangeSetting(SettingsKey.MAX_PASV_PORT, value.ToString());
            }
        }
        public static int MinPasvPort
        {
            get
            {
                return Convert.ToInt32(GetSettingsAsString(SettingsKey.MIN_PASV_PORT));
            }
            set
            {
                ChangeSetting(SettingsKey.MIN_PASV_PORT, value.ToString());
            }
        }
        public static int FTPPort
        {
            get
            {
                return Convert.ToInt32(GetSettingsAsString(SettingsKey.FTP_PORT));
            }
            set
            {
                ChangeSetting(SettingsKey.FTP_PORT, value.ToString());
            }
        }

        private static XmlDocument _Settings;
        private static string _ApplicationPath;

        /// <summary>
        /// Constructor
        /// </summary>
        public FTP()
        {
            ReadSettings();
        }

        #region Settings Functions
        /// <summary>
        /// Reads the xml, if it does not exist it will create one.
        /// </summary>
        public static void ReadSettings()
        {
            _ApplicationPath = AppDomain.CurrentDomain.BaseDirectory;
            if (!_ApplicationPath.EndsWith("\\")) _ApplicationPath += "\\";

            _Settings = new XmlDocument();

            try
            {
                if (File.Exists(_ApplicationPath + "AppSettings.xml"))
                {
                    FileStream RawStream = new FileStream(_ApplicationPath + "AppSettings.xml", FileMode.Open, FileAccess.Read);

                    byte[] Buffer = new byte[(int)RawStream.Length];
                    RawStream.Read(Buffer, 0, Buffer.Length);
                    RawStream.Close(); RawStream = null;

                    MemoryStream Stream = new MemoryStream(Buffer);
                    TextReader Reader = new StreamReader(Stream, Encoding.UTF8);

                    _Settings.Load(Reader);
                    Reader.Close(); Stream.Close();
                    Buffer = null;
                }
                else _Settings.LoadXml(Properties.Resources.SettingsXML);
            }
            catch (Exception ex) { NewServerLog($@"Error reading settings file {ex.ToString()}"); }
        }

        /// <summary>
        /// Saves all data to the xml
        /// </summary>
        public static void SaveSettings()
        {
            try
            {
                MemoryStream Stream = new MemoryStream();
                TextWriter TxtWriter = new StreamWriter(Stream, Encoding.UTF8);
                _Settings.Save(TxtWriter);

                byte[] Buff = Stream.GetBuffer();
                FileStream FS = new FileStream(_ApplicationPath + "AppSettings.xml", FileMode.Create, FileAccess.Write);
                FS.Write(Buff, 0, Buff.Length);
                FS.Close(); FS = null;
                TxtWriter.Close(); Stream.Close();
            }
            catch (Exception ex) { NewServerLog($@"Error saving settings file {ex.ToString()}"); }
            }

        /// <summary>
        /// Changes one settings key
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static void ChangeSetting(SettingsKey key, string value)
        {
            XmlNode SettingsNode = _Settings.DocumentElement.SelectSingleNode("Settings");

            foreach (XmlNode Setting in SettingsNode.ChildNodes)
            {
                if (Setting.Attributes["NAME"].Value != key.ToString()) continue;

                Setting.Attributes["VALUE"].Value = value;
                return;
            }
            XmlNode NewSetting = _Settings.CreateElement("KEY");
            XmlAttribute Attrib = _Settings.CreateAttribute("NAME");
            Attrib.Value = key.ToString();
            NewSetting.Attributes.Append(Attrib);

            Attrib = _Settings.CreateAttribute("NAME");
            Attrib.Value = value;
            NewSetting.Attributes.Append(Attrib);

            SettingsNode.AppendChild(NewSetting);
        }

        /// <summary>
        /// Returns the value of a settings key
        /// </summary>
        /// <param name="Key"></param>
        /// <returns></returns>
        static string GetSettingsAsString(SettingsKey Key)
        {
            XmlNodeList SettingsList = _Settings.DocumentElement.SelectSingleNode("Settings").ChildNodes;
            string returnValue = string.Empty;

            foreach (XmlNode Setting in SettingsList)
            {
                if (Setting.Attributes["NAME"].Value != Key.ToString()) continue;

                returnValue = Setting.Attributes["VALUE"].Value;
                break;
            }
            return returnValue;
        }

        #region UserFunctions
        /// <summary>
        /// Creates a new FTP user
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="rootpath"></param>
        /// <param name="permissions"></param>
        /// <param name="enabled"></param>
        /// <returns></returns>
        public static bool FTPCreateUser(string username, string password, string rootpath, string permissions, bool enabled)
        {
            if (FTPUserExist(username)) return false;

            XmlNodeList UserList = FTPGetUserList();
            XmlNode User = _Settings.CreateElement("USER");
            User.Attributes.Append(_Settings.CreateAttribute("UserName"));
            User.Attributes.Append(_Settings.CreateAttribute("Password"));
            User.Attributes.Append(_Settings.CreateAttribute("Rootpath"));
            User.Attributes.Append(_Settings.CreateAttribute("Permission"));
            User.Attributes.Append(_Settings.CreateAttribute("Enabled"));

            _Settings.DocumentElement.SelectSingleNode("UserAccounts").AppendChild(User);

            User.Attributes[0].Value = username;
            User.Attributes[1].Value = password;
            User.Attributes[2].Value = rootpath;
            User.Attributes[3].Value = permissions;
            User.Attributes[4].Value = (enabled) ? "1" : "0";
            SaveSettings();
            return true;
        }

        /// <summary>
        /// Edits an exisiting user
        /// </summary>
        /// <param name="oldusername"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="rootpath"></param>
        /// <param name="permissions"></param>
        /// <param name="enabled"></param>
        /// <returns></returns>
        public static bool FTPEditUser(string oldusername, string username, string password, string rootpath, string permissions, bool enabled)
        {
            string OldRootPath = string.Empty;
            XmlNodeList Users = FTPGetUserList();
            XmlNode User = FTPGetUser(oldusername);

            if (username != oldusername && FTPUserExist(username)) return false;
            else
            {
                if (username != null) User.Attributes["UserName"].Value = username;
                if (password != null) User.Attributes["Password"].Value = password;
                if (rootpath != null) User.Attributes["Rootpath"].Value = rootpath;
                if (permissions != null) User.Attributes["Permission"].Value = permissions;
                User.Attributes["Enabled"].Value = (enabled) ? "1" : "0";
                SaveSettings();
                return true;
            }
        }

        /// <summary>
        /// Deletes a FTP user
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        public static bool FTPDeleteUser(string username)
        {
            XmlNode User = FTPGetUser(username);
            if (User != null)
            {
                _Settings.DocumentElement.SelectSingleNode("UserAccounts").RemoveChild(User);
                SaveSettings();
                return true;
            }
            else return false;
        }

        /// <summary>
        /// Gets all user attributes from the xml
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="rootpath"></param>
        /// <param name="permissions"></param>
        /// <param name="enabled"></param>
        /// <returns></returns>
        public static bool FTPGetUser(string username, out string password, out string rootpath, out string permissions, out bool enabled)
        {
            XmlNode User = FTPGetUser(username);
            password = rootpath = permissions = null;
            enabled = false;
            if (User == null) return false;

            password = User.Attributes[1].Value;
            rootpath = User.Attributes[2].Value;
            permissions = User.Attributes[3].Value;
            enabled = User.Attributes[4].Value == "1";

            return true;
        }

        /// <summary>
        /// Gets a user as a XmlNode
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        public static XmlNode FTPGetUser(string username)
        {
            XmlNodeList Users = FTPGetUserList();
            XmlNode User = null;

            for (int i = 0; i < Users.Count; i++)
            {
                if (Users[i].Attributes[0].Value != username) continue;
                User = Users[i]; break;
            }

            return User;
        }

        /// <summary>
        /// Checks if an User exist
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        public static bool FTPUserExist(string username)
        {
            return FTPGetUser(username) != null;
        }

        /// <summary>
        /// Gets a list of all users
        /// </summary>
        /// <returns></returns>
        public static XmlNodeList FTPGetUserList()
        {
            return _Settings.DocumentElement.SelectNodes("UserAccounts/USER");
        }
        #endregion
        #endregion

        #region EventHandlers
        /// <summary>
        /// Sends a new Log to the UI
        /// </summary>
        /// <param name="message"></param>
        public static void NewServerLog(string message)
        {
            ServerLogArgs args = new ServerLogArgs() { Message = message };
            OnNewServerLog(args);
        }

        /// <summary>
        /// Invokes the event
        /// </summary>
        /// <param name="args"></param>
        protected static void OnNewServerLog(ServerLogArgs args)
        {
            ServerLogEvent?.Invoke(null, args);
        }
        #endregion
    }
}
