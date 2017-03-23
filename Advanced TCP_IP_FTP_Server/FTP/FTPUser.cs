using System;
using System.Xml;

namespace Advanced_TCP_IP_FTP_Server
{
    class FTPUser
    {
        public string Username { get; private set; } = "";
        public string RootDir { get; private set; } = "";
        public string WorkingDir { get; set; } = "\\";
        public bool IsAuthenticated { get; private set; }
        public bool IsEnabled { get; private set; }

        //Permissions
        public bool CanStoreFiles { get; private set; }
        public bool CanStoreFolders { get; private set; }
        public bool CanRenameFiles { get; private set; }
        public bool CanRenameFolders { get; private set; }
        public bool CanDeleteFiles { get; private set; }
        public bool CanDeleteFolders { get; private set; }
        public bool CanCopyFiles { get; private set; }
        public bool CanViewHiddenFiles { get; private set; }
        public bool CanViewHiddenFolders { get; private set; }

        private string _CorrectPassword;

        /// <summary>
        /// Loads a user profile from the xml settings file
        /// </summary>
        /// <param name="username"></param>
        public void LoadUser(string username)
        {
            try
            {
                if (Username == username || username.Length == 0) return;
                Username = username;
                IsAuthenticated = false;
                XmlNodeList Users = FTP.FTPGetUserList();

                foreach (XmlNode User in Users)
                {
                    if (User.Attributes[0].Value != username) continue;

                    _CorrectPassword = User.Attributes[1].Value;
                    RootDir = User.Attributes[2].Value;

                    char[] Permissions = User.Attributes[3].Value.ToCharArray();

                    CanStoreFiles = Permissions[0] == '1';
                    CanStoreFolders = Permissions[1] == '1';
                    CanRenameFiles = Permissions[2] == '1';
                    CanRenameFolders = Permissions[3] == '1';
                    CanDeleteFiles = Permissions[4] == '1';
                    CanDeleteFolders = Permissions[5] == '1';
                    CanCopyFiles = Permissions[6] == '1';
                    CanViewHiddenFiles = Permissions[7] == '1';
                    CanViewHiddenFolders = Permissions[8] == '1';

                    IsEnabled = User.Attributes[4].Value == "1";

                    break;
                }
            }
            catch (Exception ex) { FTP.NewServerLog($@"Error loading user {ex.ToString()}"); }
        }

        /// <summary>
        /// Checks if the given password is correct
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        public bool Authenticate(string password)
        {
            if (password == _CorrectPassword) IsAuthenticated = true;
            else IsAuthenticated = false;
            return IsAuthenticated;
        }

        /// <summary>
        /// Changes the working directory
        /// </summary>
        /// <param name="Path"></param>
        /// <returns></returns>
        public bool ChangeWorkingDir(string Path)
        {
            WorkingDir = Path;
            return true;
        }
    }
}
