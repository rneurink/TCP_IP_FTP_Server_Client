using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace Advanced_TCP_IP_FTP_Server
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        FTP ftp = new FTP();

        private string _OldUsername;

        /// <summary>
        /// Constructor
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            PortUpDown.Dispatcher.Invoke(new Action(() => PortUpDown.Value = FTP.FTPPort));

            //Sets all handlers
            FTP.ServerLogEvent += Ftp_ServerLogEvent;
            PortUpDown.ValueChanged += PortUpDown_ValueChanged;
            StartServerButton.Click += StartServerButton_Click;
            StopServerButton.Click += StopServerButton_Click;

            RefreshlistButton.Click += RefreshlistButton_Click;
            DeleteUserButton.Click += DeleteUserButton_Click;
            SaveUserButton.Click += SaveUserButton_Click;
            RootpathBrowseButton.Click += RootpathBrowseButton_Click;
        }

        #region UI Handlers
        /// <summary>
        /// Opens a browse dialog for selecting a path for the user
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RootpathBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            dialog.Multiselect = false;
            if (RootpathTB.Text != null && RootpathTB.Text != string.Empty) dialog.InitialDirectory = RootpathTB.Text;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                RootpathTB.Dispatcher.Invoke(new Action(() => RootpathTB.Text = dialog.FileName));
            }
        }

        /// <summary>
        /// Handles the double clicking on a user name to edit or create one
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListviewItem_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            string username, password, rootpath, permissions;
            username = password = rootpath = permissions = null;
            bool enabled = false;
            _OldUsername = username = ((ListViewItem)sender).Content.ToString();
            FTP.FTPGetUser(username, out password, out rootpath, out permissions, out enabled);
            ClearAndEnableUserControls(true);
            UsernameTB.Dispatcher.Invoke(new Action(() => UsernameTB.Text = username));
            PasswordTB.Dispatcher.Invoke(new Action(() => PasswordTB.Text = password));
            RootpathTB.Dispatcher.Invoke(new Action(() => RootpathTB.Text = rootpath));
            if (permissions != null)
            {
                StoreFilesCB.Dispatcher.Invoke(new Action(() => { if (permissions[0] == '1') StoreFilesCB.IsChecked = true; else StoreFilesCB.IsChecked = false; }));
                StoreFoldersCB.Dispatcher.Invoke(new Action(() => { if (permissions[1] == '1') StoreFoldersCB.IsChecked = true; else StoreFoldersCB.IsChecked = false; }));
                RenameFilesCB.Dispatcher.Invoke(new Action(() => { if (permissions[2] == '1') RenameFilesCB.IsChecked = true; else RenameFilesCB.IsChecked = false; }));
                RenameFoldersCB.Dispatcher.Invoke(new Action(() => { if (permissions[3] == '1') RenameFoldersCB.IsChecked = true; else RenameFoldersCB.IsChecked = false; }));
                DeleteFilesCB.Dispatcher.Invoke(new Action(() => { if (permissions[4] == '1') DeleteFilesCB.IsChecked = true; else DeleteFilesCB.IsChecked = false; }));
                DeleteFoldersCB.Dispatcher.Invoke(new Action(() => { if (permissions[5] == '1') DeleteFoldersCB.IsChecked = true; else DeleteFoldersCB.IsChecked = false; }));
                CopyFilesCB.Dispatcher.Invoke(new Action(() => { if (permissions[6] == '1') CopyFilesCB.IsChecked = true; else CopyFilesCB.IsChecked = false; }));
                ViewHiddenFilesCB.Dispatcher.Invoke(new Action(() => { if (permissions[7] == '1') ViewHiddenFilesCB.IsChecked = true; else ViewHiddenFilesCB.IsChecked = false; }));
                ViewHiddenFoldersCB.Dispatcher.Invoke(new Action(() => { if (permissions[8] == '1') ViewHiddenFoldersCB.IsChecked = true; else ViewHiddenFoldersCB.IsChecked = false; }));
            }
            IsEnabledCB.Dispatcher.Invoke(new Action(() => IsEnabledCB.IsChecked = enabled));
        }

        /// <summary>
        /// Saves the user
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveUserButton_Click(object sender, RoutedEventArgs e)
        {
            char[] permissions = new char[9];
            permissions[0] = (bool)StoreFilesCB.IsChecked ? '1' : '0';
            permissions[1] = (bool)StoreFoldersCB.IsChecked ? '1' : '0';
            permissions[2] = (bool)RenameFilesCB.IsChecked ? '1' : '0';
            permissions[3] = (bool)RenameFoldersCB.IsChecked ? '1' : '0';
            permissions[4] = (bool)DeleteFilesCB.IsChecked ? '1' : '0';
            permissions[5] = (bool)DeleteFoldersCB.IsChecked ? '1' : '0';
            permissions[6] = (bool)CopyFilesCB.IsChecked ? '1' : '0';
            permissions[7] = (bool)ViewHiddenFilesCB.IsChecked ? '1' : '0';
            permissions[8] = (bool)ViewHiddenFoldersCB.IsChecked ? '1' : '0';
            string permissionsString = new string(permissions);
            if (FTP.FTPUserExist(_OldUsername)) FTP.FTPEditUser(_OldUsername, UsernameTB.Text, PasswordTB.Text, RootpathTB.Text, permissionsString, (bool)IsEnabledCB.IsChecked);
            else { FTP.FTPCreateUser(UsernameTB.Text, PasswordTB.Text, RootpathTB.Text, permissionsString, (bool)IsEnabledCB.IsChecked); RefreshlistButton_Click(this, null); }
            ClearAndEnableUserControls(false);
        }

        /// <summary>
        /// Deletes a user
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteUserButton_Click(object sender, RoutedEventArgs e)
        {
            if (!FTP.FTPDeleteUser(UserList.SelectedItem.ToString())) MessageBox.Show("Unable to delete user");
            else RefreshlistButton_Click(this, null);
            ClearAndEnableUserControls(false);
        }

        /// <summary>
        /// Gets a list of all users
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RefreshlistButton_Click(object sender, RoutedEventArgs e)
        {
            XmlNodeList _UserList = FTP.FTPGetUserList();
            UserList.Dispatcher.Invoke(new Action(() => UserList.Items.Clear()));
            foreach (XmlNode node in _UserList)
            {
                UserList.Dispatcher.Invoke(new Action(() => UserList.Items.Add(node.Attributes[0].Value)));
            }
            UserList.Dispatcher.Invoke(new Action(() => UserList.Items.Add("New user")));
            ClearAndEnableUserControls(false);
        }

        /// <summary>
        /// Stops the server
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StopServerButton_Click(object sender, RoutedEventArgs e)
        {
            FTP.FtpServer.StopServer();
            EnableUIControls(false);
        }

        /// <summary>
        /// Starts the server
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StartServerButton_Click(object sender, RoutedEventArgs e)
        {
            FTP.FtpServer = new FTPServer();
            FTP.FtpServer.StartServer();
            EnableUIControls(true);
        }

        /// <summary>
        /// Changes the ftp port
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PortUpDown_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            FTP.ChangeSetting(SettingsKey.FTP_PORT, e.NewValue.ToString());
        }

        /// <summary>
        /// Gets the user list when the window is loaded
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshlistButton_Click(this, null);
        }
        #endregion

        #region Helper functions
        /// <summary>
        /// Helper function to enable/disable server controls
        /// </summary>
        /// <param name="enable"></param>
        private void EnableUIControls(bool enable)
        {
            StartServerButton.Dispatcher.Invoke(new Action(() => StartServerButton.IsEnabled = !enable));
            StopServerButton.Dispatcher.Invoke(new Action(() => StopServerButton.IsEnabled = enable));
            PortUpDown.Dispatcher.Invoke(new Action(() => PortUpDown.IsEnabled = !enable));
        }

        /// <summary>
        /// Helper function to enable/disable and clear user controls
        /// </summary>
        /// <param name="enable"></param>
        private void ClearAndEnableUserControls(bool enable)
        {
            UserControls.Dispatcher.Invoke(new Action(() => UserControls.IsEnabled = enable));
            UsernameTB.Dispatcher.Invoke(new Action(() => UsernameTB.Text = string.Empty));
            PasswordTB.Dispatcher.Invoke(new Action(() => PasswordTB.Text = string.Empty));
            RootpathTB.Dispatcher.Invoke(new Action(() => RootpathTB.Text = string.Empty));
            StoreFilesCB.Dispatcher.Invoke(new Action(() => StoreFilesCB.IsChecked = true));
            StoreFoldersCB.Dispatcher.Invoke(new Action(() => StoreFoldersCB.IsChecked = true));
            RenameFilesCB.Dispatcher.Invoke(new Action(() => RenameFilesCB.IsChecked = true));
            RenameFoldersCB.Dispatcher.Invoke(new Action(() => RenameFoldersCB.IsChecked = true));
            DeleteFilesCB.Dispatcher.Invoke(new Action(() => DeleteFilesCB.IsChecked = true));
            DeleteFoldersCB.Dispatcher.Invoke(new Action(() => DeleteFoldersCB.IsChecked = true));
            ViewHiddenFilesCB.Dispatcher.Invoke(new Action(() => ViewHiddenFilesCB.IsChecked = false));
            ViewHiddenFoldersCB.Dispatcher.Invoke(new Action(() => ViewHiddenFoldersCB.IsChecked = false));
            CopyFilesCB.Dispatcher.Invoke(new Action(() => CopyFilesCB.IsChecked = true));
            IsEnabledCB.Dispatcher.Invoke(new Action(() => IsEnabledCB.IsChecked = true));
        }
        #endregion

        #region EventHandlers
        /// <summary>
        /// Handles the ServerLog Event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void Ftp_ServerLogEvent(object sender, ServerLogArgs args)
        {
            ServerLog.Dispatcher.Invoke(new Action(() =>
            {
                ServerLog.AppendText(args.Message + Environment.NewLine);
                ServerLog.Focus();
                ServerLog.CaretIndex = ServerLog.Text.Length;
                ServerLog.ScrollToEnd();
            }));
        }
        #endregion
    }
}
