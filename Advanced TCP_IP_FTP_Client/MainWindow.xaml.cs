using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Advanced_TCP_IP_FTP_Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private FTPClient ftpclient = new FTPClient();

        public MainWindow()
        {
            InitializeComponent();
            FTPClient.ClientLogEvent += FTPClient_ClientLogEvent;
        }


        #region EventHandlers
        private void FTPClient_ClientLogEvent(object sender, ClientLogArgs args)
        {
            ClientLog.Dispatcher.Invoke(new Action(() =>
            {
                ClientLog.AppendText(args.Message + Environment.NewLine);
                ClientLog.Focus();
                ClientLog.CaretIndex = ClientLog.Text.Length;
                ClientLog.ScrollToEnd();
            }));
        }
        #endregion
    }
}
