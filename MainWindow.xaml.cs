using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using IpHlpApidotnet;
using MaxMind.GeoIP2;
using Timer = System.Timers.Timer;

namespace ConnectionMonitor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Used for geo-location
        private DatabaseReader _reader = new DatabaseReader("GeoLite2-City.mmdb");

        // Used for periodically refresh the connections list
        private BackgroundWorker worker;

        private TCPUDPConnections conns;

        public MainWindow()
        {
            InitializeComponent();

            //Get list of connections
            conns = new TCPUDPConnections();
            conns.Refresh();

            //Set up task that refreshes connection periodically
            worker = new BackgroundWorker();
            worker.DoWork += WorkerRefreshConnections;
            Timer timer = new Timer(1000);
            timer.Elapsed += TimerElapsed;
            timer.Start();

            ConnectionsDataGrid.ItemsSource = new ObservableCollection<TCPUDPConnection>(conns);

        }

        void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (!worker.IsBusy)
                worker.RunWorkerAsync();
        }

        void WorkerRefreshConnections(object sender, DoWorkEventArgs e)
        {
            // Needed when updating UI elements from outside main thread
            this.Dispatcher.Invoke((Action)(() =>
            {
                // Refresh connections
                conns.Refresh();
                ConnectionsDataGrid.Items.Refresh();

                // Update summary information
                UpdateSummary();
            }));

        }

       void UpdateSummary()
        {
            //Total number of connections
            TotalConnectionsText.Text = conns.Count().ToString();

            //Number of unique processes
            ActiveProcessesText.Text = conns.GroupBy(conn => conn.PID).Count().ToString();

            //IPv4
            V4ConnectionsText.Text = conns.Count(conn => conn.AddressFamily == AddressFamily.InterNetwork).ToString();

            //IPv6
            V6ConnectionsText.Text = conns.Count(conn => conn.AddressFamily == AddressFamily.InterNetworkV6).ToString();

            //TCP
            TcpConnectionsText.Text = conns.Count(conn => conn.Protocol == Protocol.TCP).ToString();

            //UDP
            UdpConnectionsText.Text = conns.Count(conn => conn.Protocol == Protocol.UDP).ToString();

            //Unique hosts
            UniqueHostsText.Text = conns.GroupBy(conn => IsIpAddress(conn.RemoteAddress)? GetIpWithoutPort(conn.RemoteAddress):conn.RemoteAddress).Count().ToString();
            

        }

        private void Row_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Check we have double clicked on a row in the DataGrid
            DataGridRow row = ItemsControl.ContainerFromElement((DataGrid)sender, e.OriginalSource as DependencyObject) as DataGridRow;
            if (row == null) return;

            //Get connection represented by row
            TCPUDPConnection conn = row.Item as TCPUDPConnection;

            if (conn != null)
            {
                //Update detailed view
                UpdateDetailView(conn);

            }

        }

        // Displays details of a selected connection
        private void UpdateDetailView(TCPUDPConnection conn)
        {
            PromptText.Visibility = Visibility.Hidden;

            // Clear all text
            ProtocolText.Text = string.Empty;
            StatusText.Text = string.Empty;
            LocalAddrText.Text = string.Empty;
            RemoteAddrText.Text = string.Empty;
            PIDText.Text = string.Empty;
            PNameText.Text = string.Empty;
            PPathText.Text = string.Empty;
            CountryText.Text = string.Empty;
            RegionText.Text = string.Empty;
            WhoisText.Text = string.Empty;

            ProtocolText.Inlines.Add(new Bold(new Run("Protocol ")));
            ProtocolText.Inlines.Add(conn.Protocol.ToString());

            StatusText.Inlines.Add(new Bold(new Run("Status ")));
            StatusText.Inlines.Add(conn.State);

            LocalAddrText.Inlines.Add(new Bold(new Run("Local address ")));
            LocalAddrText.Inlines.Add(conn.LocalAddress);

            RemoteAddrText.Inlines.Add(new Bold(new Run("Remote address ")));
            RemoteAddrText.Inlines.Add(conn.RemoteAddress);
            
            PIDText.Inlines.Add(new Bold(new Run("Process ID ")));
            PIDText.Inlines.Add(conn.PID.ToString());

            PNameText.Inlines.Add(new Bold(new Run("Process name ")));
            PNameText.Inlines.Add(conn.ProcessName);

            PPathText.Inlines.Add(new Bold(new Run("Process path ")));
            PPathText.Inlines.Add(conn.ProcessPath);

            
            // Allow WHOIS lookup for valid IPs that begin with a number
            String ip = conn.RemoteAddress;
            if (IsIpAddress(ip))
            {
                // Look up information about IP in database

                try
                {
                    var city = _reader.City(GetIpWithoutPort(ip));
                    CountryText.Inlines.Add(new Bold(new Run("Country ")));
                    CountryText.Inlines.Add(city.Country.Name);

                    RegionText.Inlines.Add(new Bold(new Run("Region ")));
                    RegionText.Inlines.Add(city.MostSpecificSubdivision.Name);

                }
                catch (MaxMind.GeoIP2.Exceptions.AddressNotFoundException)
                {
                    //CountryText.Inlines.Add("Unknown/Not applicable");
                }
                catch (System.ArgumentNullException)
                {
                    // Do nothing
                }

                // Display WHOIS link
                Hyperlink link = new Hyperlink();
                link.Inlines.Add("Perform WHOIS Lookup");
                link.NavigateUri = new Uri("https://who.is/whois-ip/ip-address/" + GetIpWithoutPort(ip));
                link.RequestNavigate += new System.Windows.Navigation.RequestNavigateEventHandler(Hyperlink_RequestNavigate);
                WhoisText.Inlines.Add(link);
            }

        }

        private bool IsIpAddress(String ip)
        {
            return Regex.IsMatch(ip, @"^\d+");
        }

        // Removes the port from the end of a IP + port string
        private String GetIpWithoutPort(String ip)
        {
            int indexOfLastColon = ip.LastIndexOf(':');
            return ip.Substring(0, indexOfLastColon);
        }

        // Launch web browser when link is clicked. From http://stackoverflow.com/a/10238715
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }





}
