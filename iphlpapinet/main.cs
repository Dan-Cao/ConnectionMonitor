using System;
using System.Net;
using System.Text;
using System.Collections;
using System.Management;
using System.Diagnostics;
using System.Threading;
using System.Net;
using System.Net.NetworkInformation;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Sockets;

namespace IpHlpApidotnet
{
    public enum Protocol { TCP, UDP, None };
    /// <summary>
    /// Store information concerning single TCP/UDP connection
    /// </summary>
    public class TCPUDPConnection
    {
        private int _dwState;
        public int iState
        {
            get { return _dwState; }
            set 
            {
                if (_dwState != value)
                {
                    _dwState = value;
                    _State = Utils.StateToStr(value);
                }
            }
        }

        private static TCPUDPConnections _conns = null;
        public TCPUDPConnection(TCPUDPConnections conns) : base()
        {
            _conns = conns;
        }

        public string GetHostName(IPEndPoint HostAddress)
        {
            return Utils.GetHostName(HostAddress, _conns.LocalHostName);
        }

        private bool _IsResolveIP = true;
        public bool IsResolveIP
        {
            get { return _IsResolveIP; }
            set { _IsResolveIP = value; }
        }

        private AddressFamily _addressFamily;

        public AddressFamily AddressFamily
        {
            get { return _addressFamily;}
            set { _addressFamily = value; }
        }

        private String _ProcessPath = "Unknown";

        public String ProcessPath
        {
            get { return _ProcessPath; }
            set { _ProcessPath = value; }
            
        }

        private Protocol _Protocol;
        public Protocol Protocol
        {
            get { return _Protocol; }
            set { _Protocol = value; }
        }

        private string _State = String.Empty; 
        public string State
        {
            get { return _State; }
        }

        private IPEndPoint _OldLocalHostName;
        private IPEndPoint _OldRemoteHostName;
        private string _LocalAddress = String.Empty;
        private string _RemoteAddress = String.Empty;
        private void SaveHostName(bool IsLocalHostName)
        {
            
            if (IsLocalHostName)
            {
                this._LocalAddress = GetHostName(this._Local);
                this._OldLocalHostName = this._Local;
            }
            else
            {
                if (this.Protocol == Protocol.TCP)
                {
                    this._RemoteAddress = GetHostName(this._Remote);
                }
                else
                {
                    this._RemoteAddress = "";
                }
                
                this._OldRemoteHostName = this._Remote;
            }
        }

        public string LocalAddress
        {
            get
            {
                if (this._OldLocalHostName == this._Local)
                {
                    if (this._LocalAddress.Trim() == String.Empty)
                    {
                        this.SaveHostName(true);
                    }
                }
                else
                {
                    this.SaveHostName(true);
                }
                return this._LocalAddress;
            }
        }

        public string RemoteAddress
        {
            get
            {
                if (this._OldRemoteHostName == this._Remote)
                {
                    if (this._RemoteAddress.Trim() == String.Empty)
                    {
                        this.SaveHostName(false);
                    }
                }
                else
                {
                    this.SaveHostName(false);
                }
                return this._RemoteAddress;
            }
        }

        private IPEndPoint _Local = null;
        public IPEndPoint Local  //LocalAddress
        {
            get { return this._Local; }
            set
            {
                if (this._Local != value)
                {
                    this._Local = value;
                }
            }
        }

        private IPEndPoint _Remote;
        public IPEndPoint Remote //RemoteAddress
        {
            get { return this._Remote; }
            set 
            {
                if (this._Remote != value)
                {
                    this._Remote = value;
                }
            }
        }

        private int _dwOwningPid;
        public int PID
        {
            get { return this._dwOwningPid; }
            set
            {
                if (this._dwOwningPid != value)
                {
                    this._dwOwningPid = value;
                }
            }
        }

        // Sets process information
        private void SaveProcessID()
        {
            this._ProcessName = Utils.GetProcessNameByPID(this._dwOwningPid);

            // Find process name if TCP. Does not work for UDP as IPHelper does not provide PID
            if (Protocol.Equals(Protocol.TCP))
            {
                //Debug.Print("Trying PID " + _dwOwningPid );
                this._ProcessPath = Utils.GetProcessPathByPID(this._dwOwningPid);
            }

            this._OldProcessID = this._dwOwningPid;

        }

        private int _OldProcessID = -1;
        private string _ProcessName = String.Empty;
        public string ProcessName
        {
            get
            {
                if (this._OldProcessID == this._dwOwningPid)
                {
                    if (this._ProcessName.Trim() == String.Empty)
                    {
                        this.SaveProcessID();
                    }
                }
                else
                {
                    this.SaveProcessID();
                }
                return this._ProcessName;
            }
        }

        private DateTime _WasActiveAt = DateTime.MinValue;
        public DateTime WasActiveAt
        {
            get { return _WasActiveAt; }
            internal set { _WasActiveAt = value; }
        }

        private Object _Tag = null;
        public Object Tag
        {
            get { return this._Tag; }
            set { this._Tag = value; }
        }
    }

    public class SortConnections : IComparer<TCPUDPConnection>
    {
        /// <summary>
        /// Method is used to compare two <seealso cref="TCPUDPConnection"/>. 
        /// 
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <returns></returns>
        public virtual int CompareConnections(TCPUDPConnection first, TCPUDPConnection second)
        {
            int i;
            i = Utils.CompareIPEndPoints(first.Local, second.Local);
            if (i != 0)
                    return i;
            if (first.Protocol == Protocol.TCP &&
                second.Protocol == Protocol.TCP)
            {
                i = Utils.CompareIPEndPoints(first.Remote, second.Remote);
                if (i != 0)
                    return i;
            }
            i = first.PID - second.PID;
            if (i != 0)
                return i;
            if (first.Protocol == second.Protocol)
                return 0;
            if (first.Protocol == Protocol.TCP)
                return -1;
            else
                return 1;
        }

        #region IComparer<TCPUDPConnection> Members

        public int Compare(TCPUDPConnection x, TCPUDPConnection y)
        {
            return this.CompareConnections(x, y);
        }

        #endregion
    }


    /// <summary>
    /// Store information concerning TCP/UDP connections
    /// </summary>
    public class TCPUDPConnections : IEnumerable<TCPUDPConnection>
    {
        private List<TCPUDPConnection> _list;
        System.Timers.Timer _timer = null;
        private int _DeadConnsMultiplier = 10; //Collect dead connections each 5 sec.
        private int _TimerCounter = -1;
        private string _LocalHostName = String.Empty;

        public TCPUDPConnections()
        {
            _LocalHostName = Utils.GetLocalHostName();
            _list = new List<TCPUDPConnection>();
            _timer = new System.Timers.Timer();
            _timer.Interval = 1000; // Refresh list every 1 sec.
            _timer.Elapsed += new System.Timers.ElapsedEventHandler(_timer_Elapsed);
            _timer.Start();
        }

        /// <summary>
        /// Coefficient multiplies on AutoRefresh timer interval. The parameter determinate how 
        /// often detecting of dead connections occures.
        /// </summary>
        public int DeadConnsMultiplier
        {
            get { return _DeadConnsMultiplier; }
            set { _DeadConnsMultiplier = value; }
        }

        /// <summary>
        /// AutoRefresh timer. 
        /// </summary>
        public System.Timers.Timer Timer
        {
            get { return _timer; }
        }

        public delegate void ItemAddedEvent(Object sender, TCPUDPConnection item);
        /// <summary>
        /// Event occures when <seealso cref="TCPUDPConnection"/> deleted.
        /// </summary>
        public event ItemAddedEvent ItemAdded;
        private void ItemAddedEventHandler(TCPUDPConnection item)
        {
            if (ItemAdded != null)
            {
                ItemAdded(this, item);
            }
        }

        public delegate void ItemChangedEvent(Object sender, TCPUDPConnection item, int Pos);
        /// <summary>
        /// Event occures when <seealso cref="TCPUDPConnection"/> changed.
        /// </summary>
        public event ItemChangedEvent ItemChanged;
        private void ItemChangedEventHandler(TCPUDPConnection item, int Pos)
        {
            if (ItemChanged != null)
            {
                ItemChanged(this, item, Pos);
            }
        }

        public delegate void ItemInsertedEvent(Object sender, TCPUDPConnection item, int Position);
        /// <summary>
        /// Event occures when <seealso cref="TCPUDPConnection"/> inserted into list.
        /// </summary>
        public event ItemInsertedEvent ItemInserted;
        private void ItemInsertedEventHandler(TCPUDPConnection item, int Position)
        {
            if (ItemInserted != null)
            {
                ItemInserted(this, item, Position);
            }
        }

        public delegate void ItemDeletedEvent(Object sender, TCPUDPConnection item, int Position);
        /// <summary>
        /// Event occures when <seealso cref="TCPUDPConnection"/> deleted from list. 
        /// </summary>
        public event ItemDeletedEvent ItemDeleted;
        private void ItemDeletedEventHandler(TCPUDPConnection item, int Position)
        {
            if (ItemDeleted != null)
            {
                ItemDeleted(this, item, Position);
            }
        }

        void _timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            this.Refresh();
        }

        /// <summary>
        /// Refresh connections list.
        /// </summary>
        public void Refresh()
        {
            lock (this)
            {
                this._LastRefreshDateTime = DateTime.Now;
                this.GetTcpConnections();
                GetTcp6Connections();
                this.GetUdpConnections();
                _TimerCounter++;
                if (_DeadConnsMultiplier == _TimerCounter)
                {
                    this.CheckForClosedConnections();
                    _TimerCounter = -1;
                }
            }
        }

        /// <summary>
        /// Get last refresh <seealso cref="DateTime"/>.
        /// </summary>
        private DateTime _LastRefreshDateTime = DateTime.MinValue;
        public DateTime LastRefreshDateTime
        {
            get { return _LastRefreshDateTime; }
        }

        public void StopAutoRefresh()
        {
            _timer.Stop();
        }

        public void StartAutoRefresh()
        {
            _timer.Start();
        }

        /// <summary>
        /// Enable or Disable connections list auto refresh.
        /// </summary>
        public bool AutoRefresh
        {
            get { return _timer.Enabled; }
            set { _timer.Enabled = value; }
        }

        /// <summary>
        /// Add new <seealso cref="TCPUDPConnection"/> connection.
        /// </summary>
        /// <param name="item"></param>
        public void Add(TCPUDPConnection item)
        {
            int Pos = 0;
            TCPUDPConnection conn = IndexOf(item, out Pos);
            if (conn == null)
            {
                item.WasActiveAt = DateTime.Now;
                if (Pos > -1)
                {
                    this.Insert(Pos, item);
                }
                else
                {
                    _list.Add(item);
                    ItemAddedEventHandler(item);
                }
            }
            else
            {
                _list[Pos].WasActiveAt = DateTime.Now;
                if (conn.iState != item.iState ||
                    conn.PID != item.PID)
                {
                    conn.iState = item.iState;
                    conn.PID = item.PID;
                    ItemChangedEventHandler(conn, Pos);
                }
            }
        }

        public int Count
        {
            get { return _list.Count; }
        }

        public TCPUDPConnection this[int index]
        {
            get { return _list[index]; }
            set { _list[index] = value; }
        }

        private SortConnections _connComp = new SortConnections();
        public void Sort()
        {
            _list.Sort(_connComp);
        }

        public TCPUDPConnection IndexOf(TCPUDPConnection item, out int Pos)
        {
            int index = -1;
            foreach (TCPUDPConnection conn in _list)
            {
                index++;
                int i = _connComp.CompareConnections(item, conn);
                if (i == 0)
                {
                    Pos = index;
                    return conn;
                }
                if (i > 0) // If current an item more then conn, try to compare with next one until finding equal or less.
                {
                    continue; //Skip
                }
                if (i < 0) // If there is an item in list with row less then current, insert current before this one.
                {
                    Pos = index;
                    return null;
                }
            }
            Pos = -1;
            return null;
        }

        /// <summary>
        /// Method detect and remove from list all dead connections.
        /// </summary>
        public void CheckForClosedConnections()
        {
            int interval = (int)_timer.Interval * this._DeadConnsMultiplier;
            //Remove item from the end of the list
            for (int index = _list.Count - 1; index >= 0; index--)
            {
                TCPUDPConnection conn = this[index];
                TimeSpan diff = (this._LastRefreshDateTime - conn.WasActiveAt);

                int interval1 = Math.Abs((int)diff.TotalMilliseconds);
                if (interval1 > interval)
                {
                    this.Remove(index);
                }
            }
        }

        public void Remove(int index)
        {
            TCPUDPConnection conn = this[index];
            _list.RemoveAt(index);
            this.ItemDeletedEventHandler(conn, index);
        }

        public void Insert(int index, TCPUDPConnection item)
        {
            _list.Insert(index, item);
            ItemInsertedEventHandler(item, index);
        }


        
        public void GetTcpConnections()
        {
            int AF_INET = 2;    // IP_v4
            int buffSize = 20000;
            byte[] buffer = new byte[buffSize];
            int res = IPHlpAPI32Wrapper.GetExtendedTcpTable(buffer, out buffSize, true, AF_INET, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);
            if (res != Utils.NO_ERROR) //If there is no enouth memory to execute function
            {
                buffer = new byte[buffSize];
                res = IPHlpAPI32Wrapper.GetExtendedTcpTable(buffer, out buffSize, true, AF_INET, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);
                if (res != Utils.NO_ERROR)
                {
                    return;
                }
            }
            int nOffset = 0;
            // number of entry in the
            int NumEntries = Convert.ToInt32(buffer[nOffset]);
            nOffset += 4;
            for (int i = 0; i < NumEntries; i++)
            {
                TCPUDPConnection row = new TCPUDPConnection(this);
                // state
                int st = Convert.ToInt32(buffer[nOffset]);
                // state  by ID
                row.iState = st;
                nOffset += 4;
                row.Protocol = Protocol.TCP;
                row.AddressFamily = AddressFamily.InterNetwork;
                row.Local = Utils.BufferToIPEndPoint(buffer, ref nOffset, false);
                row.Remote = Utils.BufferToIPEndPoint(buffer, ref nOffset, true);
                row.PID = Utils.BufferToInt(buffer, ref nOffset);
                this.Add(row);
            }

            
        }
        // New IPv6 version
        public void GetTcp6Connections()
        {
            int AF_INET = (int)AddressFamily.InterNetworkV6;    // AF_INET6
            int buffSize = 20000;
            byte[] buffer = new byte[buffSize];
            int res = IPHlpAPI32Wrapper.GetExtendedTcpTable(buffer, out buffSize, true, AF_INET, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);
            if (res != Utils.NO_ERROR) //If there is no enough memory to execute function
            {
                buffer = new byte[buffSize];
                res = IPHlpAPI32Wrapper.GetExtendedTcpTable(buffer, out buffSize, true, AF_INET, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);
                if (res != Utils.NO_ERROR)
                {
                    return;
                }
            }
            int nOffset = 0;
            // number of entries in the table
            int NumEntries = Convert.ToInt32(buffer[nOffset]);
            nOffset += 4;
            for (int i = 0; i < NumEntries; i++)
            {
                TCPUDPConnection row = new TCPUDPConnection(this);
                row.Protocol = Protocol.TCP;
                row.AddressFamily = AddressFamily.InterNetworkV6;

                // Read in local address information
                row.Local = Utils.BufferToIPv6EndPoint(buffer, ref nOffset);

                // Read in remote address information
                row.Remote = Utils.BufferToIPv6EndPoint(buffer, ref nOffset);

                // State
                int state = BitConverter.ToInt32(buffer, nOffset);
                row.iState = state;
                nOffset += 4;

                // PID
                row.PID = BitConverter.ToInt32(buffer, nOffset);
                nOffset += 4;

                // Add row to list of connections
                Add(row);
                
            }
            
        }



        public string LocalHostName
        {
            get { return _LocalHostName; }
        }

        public void GetUdpConnections()
        {
            int AF_INET = 2;    // IP_v4
            int buffSize = 20000;
            byte[] buffer = new byte[buffSize];
            int res = IPHlpAPI32Wrapper.GetExtendedUdpTable(buffer, out buffSize, true, AF_INET, UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID, 0);
            if (res != Utils.NO_ERROR)
            {
                buffer = new byte[buffSize];
                res = IPHlpAPI32Wrapper.GetExtendedUdpTable(buffer, out buffSize, true, AF_INET, UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID, 0);
                if (res != Utils.NO_ERROR)
                {
                    return;
                }
            }
            int nOffset = 0;
            int NumEntries = Convert.ToInt32(buffer[nOffset]);
            nOffset += 4;
            for (int i = 0; i < NumEntries; i++)
            {
                TCPUDPConnection row = new TCPUDPConnection(this);
                row.Protocol = Protocol.UDP;
                row.AddressFamily = AddressFamily.InterNetwork;
                row.Local = Utils.BufferToIPEndPoint(buffer, ref nOffset, false);
                row.PID = Utils.BufferToInt(buffer, ref nOffset);
                this.Add(row);

            }
        }
        
        //IPv6 Version
        public void GetUdp6Connections()
        {
            int AF_INET = (int) AddressFamily.InterNetworkV6;    // IP_v6
            int buffSize = 20000;
            byte[] buffer = new byte[buffSize];
            int res = IPHlpAPI32Wrapper.GetExtendedUdpTable(buffer, out buffSize, true, AF_INET, UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID, 0);
            if (res != Utils.NO_ERROR)
            {
                buffer = new byte[buffSize];
                res = IPHlpAPI32Wrapper.GetExtendedUdpTable(buffer, out buffSize, true, AF_INET, UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID, 0);
                if (res != Utils.NO_ERROR)
                {
                    return;
                }
            }
            int nOffset = 0;
            int NumEntries = Convert.ToInt32(buffer[nOffset]);
            nOffset += 4;
            for (int i = 0; i < NumEntries; i++)
            {
                TCPUDPConnection row = new TCPUDPConnection(this);
                row.Protocol = Protocol.UDP;
                row.AddressFamily = AddressFamily.InterNetworkV6;

                // Read in local address information
                row.Local = Utils.BufferToIPv6EndPoint(buffer, ref nOffset);

                // PID
                row.PID = BitConverter.ToInt32(buffer, nOffset);
                nOffset += 4;

                this.Add(row);
            }
        }
        #region IEnumerable<TCPUDPConnection> Members

        public IEnumerator<TCPUDPConnection> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        #endregion
    }

    public class IPHelper
    {
        /*
         * Tcp Struct
         * */
        public IpHlpApidotnet.MIB_TCPTABLE TcpConnections;
        public IpHlpApidotnet.MIB_TCPSTATS TcpStats;
        public IpHlpApidotnet.MIB_EXTCPTABLE TcpExConnections;
        //public IpHlpApidotnet.MIB_TCPTABLE_OWNER_PID TcpExAllConnections;

        /*
         * Udp Struct
         * */
        public IpHlpApidotnet.MIB_UDPSTATS UdpStats;
        public IpHlpApidotnet.MIB_UDPTABLE UdpConnections;
        public IpHlpApidotnet.MIB_EXUDPTABLE UdpExConnections;
        //public IpHlpApidotnet.MIB_UDPTABLE_OWNER_PID UdpExAllConnections;

        public TCPUDPConnections Connections; 

        public IPHelper()
        {

        }

        #region Tcp Functions

        public void GetTcpStats()
        {
            TcpStats = new MIB_TCPSTATS();
            IPHlpAPI32Wrapper.GetTcpStatistics(ref TcpStats);
        }

        public void GetExTcpConnections()
        {
            // the size of the MIB_EXTCPROW struct =  6*DWORD
            int rowsize = 24;
            int BufferSize = 100000;
            // allocate a dumb memory space in order to retrieve  nb of connection
            IntPtr lpTable = Marshal.AllocHGlobal(BufferSize);
            //getting infos
            int res = IPHlpAPI32Wrapper.AllocateAndGetTcpExTableFromStack(ref lpTable, true, IPHlpAPI32Wrapper.GetProcessHeap(), 0, 2);
            if (res != Utils.NO_ERROR)
            {
                Debug.WriteLine("Error : " + IPHlpAPI32Wrapper.GetAPIErrorMessageDescription(res) + " " + res);
                return; // Error. You should handle it
            }
            int CurrentIndex = 0;
            //get the number of entries in the table
            int NumEntries = (int)Marshal.ReadIntPtr(lpTable);
            lpTable = IntPtr.Zero;
            // free allocated space in memory
            Marshal.FreeHGlobal(lpTable);
            ///////////////////
            // calculate the real buffer size nb of entrie * size of the struct for each entrie(24) + the dwNumEntries
            BufferSize = (NumEntries * rowsize) + 4;
            // make the struct to hold the resullts
            TcpExConnections = new IpHlpApidotnet.MIB_EXTCPTABLE();
            // Allocate memory
            lpTable = Marshal.AllocHGlobal(BufferSize);
            res = IPHlpAPI32Wrapper.AllocateAndGetTcpExTableFromStack(ref lpTable, true, IPHlpAPI32Wrapper.GetProcessHeap(), 0, 2);
            if (res != Utils.NO_ERROR)
            {
                Debug.WriteLine("Error : " + IPHlpAPI32Wrapper.GetAPIErrorMessageDescription(res) + " " + res);
                return; // Error. You should handle it
            }
            // New pointer of iterating throught the data
            IntPtr current = lpTable;
            CurrentIndex = 0;
            // get the (again) the number of entries
            NumEntries = (int)Marshal.ReadIntPtr(current);
            TcpExConnections.dwNumEntries = NumEntries;
            // Make the array of entries
            TcpExConnections.table = new MIB_EXTCPROW[NumEntries];
            // iterate the pointer of 4 (the size of the DWORD dwNumEntries)
            CurrentIndex += 4;
            current = (IntPtr)((int)current + CurrentIndex);
            // for each entries
            for (int i = 0; i < NumEntries; i++)
            {

                // The state of the connection (in string)
                TcpExConnections.table[i].StrgState = Utils.StateToStr((int)Marshal.ReadIntPtr(current));
                // The state of the connection (in ID)
                TcpExConnections.table[i].iState = (int)Marshal.ReadIntPtr(current);
                // iterate the pointer of 4
                current = (IntPtr)((int)current + 4);
                // get the local address of the connection
                UInt32 localAddr = (UInt32)Marshal.ReadIntPtr(current);
                // iterate the pointer of 4
                current = (IntPtr)((int)current + 4);
                // get the local port of the connection
                UInt32 localPort = (UInt32)Marshal.ReadIntPtr(current);
                // iterate the pointer of 4
                current = (IntPtr)((int)current + 4);
                // Store the local endpoint in the struct and convertthe port in decimal (ie convert_Port())
                TcpExConnections.table[i].Local = new IPEndPoint(localAddr, (int)Utils.ConvertPort(localPort));
                // get the remote address of the connection
                UInt32 RemoteAddr = (UInt32)Marshal.ReadIntPtr(current);
                // iterate the pointer of 4
                current = (IntPtr)((int)current + 4);
                UInt32 RemotePort = 0;
                // if the remote address = 0 (0.0.0.0) the remote port is always 0
                // else get the remote port
                if (RemoteAddr != 0)
                {
                    RemotePort = (UInt32)Marshal.ReadIntPtr(current);
                    RemotePort = Utils.ConvertPort(RemotePort);
                }
                current = (IntPtr)((int)current + 4);
                // store the remote endpoint in the struct  and convertthe port in decimal (ie convert_Port())
                TcpExConnections.table[i].Remote = new IPEndPoint(RemoteAddr, (int)RemotePort);
                // store the process ID
                TcpExConnections.table[i].dwProcessId = (int)Marshal.ReadIntPtr(current);
                // Store and get the process name in the struct
                TcpExConnections.table[i].ProcessName = Utils.GetProcessNameByPID(TcpExConnections.table[i].dwProcessId);
                current = (IntPtr)((int)current + 4);

            }
            // free the buffer
            Marshal.FreeHGlobal(lpTable);
            // re init the pointer
            current = IntPtr.Zero;
        }

        public TcpConnectionInformation[] GetTcpConnectionsNative()
        {
            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
            return properties.GetActiveTcpConnections();
        }

        public IPEndPoint[] GetUdpListeners()
        {
            return IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners();
        }

        public IPEndPoint[] GetTcpListeners()
        {
            return IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
        }

        public void GetTcpConnections()
        {
            byte[] buffer = new byte[20000]; // Start with 20.000 bytes left for information about tcp table
            int pdwSize = 20000;
            int res = IPHlpAPI32Wrapper.GetTcpTable(buffer, out pdwSize, true);
            if (res != Utils.NO_ERROR)
            {
                buffer = new byte[pdwSize];
                res = IPHlpAPI32Wrapper.GetTcpTable(buffer, out pdwSize, true);
                if (res != 0)
                    return;     // Error. You should handle it
            }

            TcpConnections = new IpHlpApidotnet.MIB_TCPTABLE();

            int nOffset = 0;
            // number of entry in the
            TcpConnections.dwNumEntries = Convert.ToInt32(buffer[nOffset]);
            nOffset += 4;
            TcpConnections.table = new MIB_TCPROW[TcpConnections.dwNumEntries];

            for (int i = 0; i < TcpConnections.dwNumEntries; i++)
            {
                // state
                int st = Convert.ToInt32(buffer[nOffset]);
                // state in string
                //((MIB_TCPROW)(TcpConnections.table[i])).StrgState = Utils.StateToStr(st);
                (TcpConnections.table[i]).StrgState = Utils.StateToStr(st);
                // state  by ID
                //((MIB_TCPROW)(TcpConnections.table[i])).iState = st;
                (TcpConnections.table[i]).iState = st;
                nOffset += 4;
                // local address
                //((MIB_TCPROW)(TcpConnections.table[i])).Local = Utils.BufferToIPEndPoint(buffer, ref nOffset, false);
                (TcpConnections.table[i]).Local = Utils.BufferToIPEndPoint(buffer, ref nOffset, false);
                // remote address
                //((MIB_TCPROW)(TcpConnections.table[i])).Remote = Utils.BufferToIPEndPoint(buffer, ref nOffset, true);
                (TcpConnections.table[i]).Remote = Utils.BufferToIPEndPoint(buffer, ref nOffset, true);
            }
        }

        #endregion

        #region Udp Functions

        public void GetUdpStats()
        {

            UdpStats = new MIB_UDPSTATS();
            IPHlpAPI32Wrapper.GetUdpStatistics(ref UdpStats);
        }

        public void GetUdpConnections()
        {
            byte[] buffer = new byte[20000]; // Start with 20.000 bytes left for information about tcp table
            int pdwSize = 20000;
            int res = IPHlpAPI32Wrapper.GetUdpTable(buffer, out pdwSize, true);
            if (res != Utils.NO_ERROR)
            {
                buffer = new byte[pdwSize];
                res = IPHlpAPI32Wrapper.GetUdpTable(buffer, out pdwSize, true);
                if (res != Utils.NO_ERROR)
                    return;     // Error. You should handle it
            }

            UdpConnections = new IpHlpApidotnet.MIB_UDPTABLE();

            int nOffset = 0;
            // number of entry in the
            UdpConnections.dwNumEntries = Convert.ToInt32(buffer[nOffset]);
            nOffset += 4;
            UdpConnections.table = new MIB_UDPROW[UdpConnections.dwNumEntries];
            for (int i = 0; i < UdpConnections.dwNumEntries; i++)
            {
                (UdpConnections.table[i]).Local = Utils.BufferToIPEndPoint(buffer, ref nOffset, false);//new IPEndPoint(IPAddress.Parse(LocalAdrr), LocalPort);
                //((MIB_UDPROW)(UdpConnections.table[i])).Local = Utils.BufferToIPEndPoint(buffer, ref nOffset, false);//new IPEndPoint(IPAddress.Parse(LocalAdrr), LocalPort);
            }
        }

        public void GetExUdpConnections()
        {
            // the size of the MIB_EXTCPROW struct =  4*DWORD
            int rowsize = 12;
            int BufferSize = 100000;
            // allocate a dumb memory space in order to retrieve  nb of connection
            IntPtr lpTable = Marshal.AllocHGlobal(BufferSize);
            //getting infos
            int res = IPHlpAPI32Wrapper.AllocateAndGetUdpExTableFromStack(ref lpTable, true, IPHlpAPI32Wrapper.GetProcessHeap(), 0, 2);
            if (res != Utils.NO_ERROR)
            {
                Debug.WriteLine("Error : " + IPHlpAPI32Wrapper.GetAPIErrorMessageDescription(res) + " " + res);
                return; // Error. You should handle it
            }
            int CurrentIndex = 0;
            //get the number of entries in the table
            int NumEntries = (int)Marshal.ReadIntPtr(lpTable);
            lpTable = IntPtr.Zero;
            // free allocated space in memory
            Marshal.FreeHGlobal(lpTable);

            ///////////////////
            // calculate the real buffer size nb of entrie * size of the struct for each entrie(24) + the dwNumEntries
            BufferSize = (NumEntries * rowsize) + 4;
            // make the struct to hold the resullts
            UdpExConnections = new IpHlpApidotnet.MIB_EXUDPTABLE();
            // Allocate memory
            lpTable = Marshal.AllocHGlobal(BufferSize);
            res = IPHlpAPI32Wrapper.AllocateAndGetUdpExTableFromStack(ref lpTable, true, IPHlpAPI32Wrapper.GetProcessHeap(), 0, 2);
            if (res != Utils.NO_ERROR)
            {
                Debug.WriteLine("Error : " + IPHlpAPI32Wrapper.GetAPIErrorMessageDescription(res) + " " + res);
                return; // Error. You should handle it
            }
            // New pointer of iterating throught the data
            IntPtr current = lpTable;
            CurrentIndex = 0;
            // get the (again) the number of entries
            NumEntries = (int)Marshal.ReadIntPtr(current);
            UdpExConnections.dwNumEntries = NumEntries;
            // Make the array of entries
            UdpExConnections.table = new MIB_EXUDPROW[NumEntries];
            // iterate the pointer of 4 (the size of the DWORD dwNumEntries)
            CurrentIndex += 4;
            current = (IntPtr)((int)current + CurrentIndex);
            // for each entries
            for (int i = 0; i < NumEntries; i++)
            {
                // get the local address of the connection
                UInt32 localAddr = (UInt32)Marshal.ReadIntPtr(current);
                // iterate the pointer of 4
                current = (IntPtr)((int)current + 4);
                // get the local port of the connection
                UInt32 localPort = (UInt32)Marshal.ReadIntPtr(current);
                // iterate the pointer of 4
                current = (IntPtr)((int)current + 4);
                // Store the local endpoint in the struct and convertthe port in decimal (ie convert_Port())
                UdpExConnections.table[i].Local = new IPEndPoint(localAddr, Utils.ConvertPort(localPort));
                // store the process ID
                UdpExConnections.table[i].dwProcessId = (int)Marshal.ReadIntPtr(current);
                // Store and get the process name in the struct
                UdpExConnections.table[i].ProcessName = Utils.GetProcessNameByPID(UdpExConnections.table[i].dwProcessId);
                current = (IntPtr)((int)current + 4);

            }
            // free the buffer
            Marshal.FreeHGlobal(lpTable);
            // re init the pointer
            current = IntPtr.Zero;
        }

        #endregion
    }

    public static class Utils
    {
        public const int NO_ERROR = 0;
        public const int MIB_TCP_STATE_CLOSED = 1;
        public const int MIB_TCP_STATE_LISTEN = 2;
        public const int MIB_TCP_STATE_SYN_SENT = 3;
        public const int MIB_TCP_STATE_SYN_RCVD = 4;
        public const int MIB_TCP_STATE_ESTAB = 5;
        public const int MIB_TCP_STATE_FIN_WAIT1 = 6;
        public const int MIB_TCP_STATE_FIN_WAIT2 = 7;
        public const int MIB_TCP_STATE_CLOSE_WAIT = 8;
        public const int MIB_TCP_STATE_CLOSING = 9;
        public const int MIB_TCP_STATE_LAST_ACK = 10;
        public const int MIB_TCP_STATE_TIME_WAIT = 11;
        public const int MIB_TCP_STATE_DELETE_TCB = 12;

        #region helper function

        public static UInt16 ConvertPort(UInt32 dwPort)
        {
            byte[] b = new Byte[2];
            // high weight byte
            b[0] = byte.Parse((dwPort >> 8).ToString());
            // low weight byte
            b[1] = byte.Parse((dwPort & 0xFF).ToString());
            return BitConverter.ToUInt16(b, 0);
        }

        public static int BufferToInt(byte[] buffer, ref int nOffset)
        {
            int res = (((int)buffer[nOffset])) + (((int)buffer[nOffset + 1]) << 8) +
                (((int)buffer[nOffset + 2]) << 16) + (((int)buffer[nOffset + 3]) << 24);
            nOffset += 4;
            return res;
        }

        public static string StateToStr(int state)
        {
            string strg_state = "";
            switch (state)
            {
                case MIB_TCP_STATE_CLOSED: strg_state = "CLOSED"; break;
                case MIB_TCP_STATE_LISTEN: strg_state = "LISTEN"; break;
                case MIB_TCP_STATE_SYN_SENT: strg_state = "SYN_SENT"; break;
                case MIB_TCP_STATE_SYN_RCVD: strg_state = "SYN_RCVD"; break;
                case MIB_TCP_STATE_ESTAB: strg_state = "ESTAB"; break;
                case MIB_TCP_STATE_FIN_WAIT1: strg_state = "FIN_WAIT1"; break;
                case MIB_TCP_STATE_FIN_WAIT2: strg_state = "FIN_WAIT2"; break;
                case MIB_TCP_STATE_CLOSE_WAIT: strg_state = "CLOSE_WAIT"; break;
                case MIB_TCP_STATE_CLOSING: strg_state = "CLOSING"; break;
                case MIB_TCP_STATE_LAST_ACK: strg_state = "LAST_ACK"; break;
                case MIB_TCP_STATE_TIME_WAIT: strg_state = "TIME_WAIT"; break;
                case MIB_TCP_STATE_DELETE_TCB: strg_state = "DELETE_TCB"; break;
            }
            return strg_state;
        }

        public static IPEndPoint BufferToIPEndPoint(byte[] buffer, ref int nOffset, bool IsRemote)
        {
            //address
            Int64 m_Address = ((((buffer[nOffset + 3] << 0x18) | (buffer[nOffset + 2] << 0x10)) | (buffer[nOffset + 1] << 8)) | buffer[nOffset]) & ((long)0xffffffff);
            nOffset += 4;
            int m_Port = 0;
            m_Port = (IsRemote && (m_Address == 0))? 0 : 
                        (((int)buffer[nOffset]) << 8) + (((int)buffer[nOffset + 1])) + (((int)buffer[nOffset + 2]) << 24) + (((int)buffer[nOffset + 3]) << 16);
            nOffset += 4;

            // store the remote endpoint
            IPEndPoint temp = new IPEndPoint(m_Address, m_Port);
            if (temp == null)
                Debug.WriteLine("Parsed address is null. Addr=" + m_Address.ToString() + " Port=" + m_Port + " IsRemote=" + IsRemote.ToString());
            return temp;
        }

        public static IPEndPoint BufferToIPv6EndPoint(byte[] buffer, ref int nOffset)
        {
            // Address
            byte[] address = new byte[16];
            Buffer.BlockCopy(buffer, nOffset, address, 0, 16);
            nOffset += 16;

            // Scope ID
            int scopeId = BitConverter.ToInt32(buffer, nOffset);
            nOffset += 4;

            // Port
            int port = BitConverter.ToInt32(buffer, nOffset);
            nOffset += 4;

            return new IPEndPoint(new IPAddress(address, scopeId), port);

        }

        public static string GetHostName(IPEndPoint HostAddress, string LocalHostName)
        {
            try
            {
                if (HostAddress.Address.ToString() == "0.0.0.0") //|| HostAddress.Address.ToString() == "127.0.0.1" || HostAddress.Address.ToString() == "::1")
                //if (HostAddress.Address.Address == 0)//ToString() == "0.0.0.0")
                {
                    if (HostAddress.Port > 0)
                        return LocalHostName + ":" + HostAddress.Port.ToString();
                    else
                        return "Anyone";
                }
                return HostAddress.Address.ToString() + ":" + HostAddress.Port.ToString();
                //return Dns.GetHostEntry(HostAddress.Address).HostName + ":" + HostAddress.Port.ToString();
            }
            catch
            {
                return HostAddress != null ? HostAddress.ToString() : "";
                //return HostAddress.ToString();
            }
        }

        public static string GetLocalHostName()
        {
            //IPGlobalProperties.GetIPGlobalProperties().DomainName +"." + IPGlobalProperties.GetIPGlobalProperties().HostName
            return Dns.GetHostEntry("localhost").HostName;
        }

        public static int CompareIPEndPoints(IPEndPoint first, IPEndPoint second)
        {
            int i;

            //Compare IP Address families
            i = first.AddressFamily - second.AddressFamily;
            if (i != 0)
            {
                return i;
            }

            
            byte[] _first = first.Address.GetAddressBytes();
            byte[] _second = second.Address.GetAddressBytes();
            for (int j = 0; j < _first.Length; j++)
            {
                i = _first[j] - _second[j];
                if (i != 0)
                    return i;
            }
            i = first.Port - second.Port;
            if (i != 0)
                return i;
            return 0;
        }

        public static string GetProcessNameByPID(int processID)
        {
            //could be an error here if the process die before we can get his name
            try
            {
                Process p = Process.GetProcessById((int)processID);
                return p.ProcessName;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return "Unknown";
            }
        }

        public static string GetProcessPathByPID(int processID)
        {
            
            return GetExecutablePath(Process.GetProcessById((int) processID));
           
        }

        // Code to find executable path below from http://www.codeproject.com/Articles/304668/How-to-Get-Elevated-Process-Path-in-Net
        [Flags]
        public enum ProcessAccessFlags : uint
        {
            PROCESS_ALL_ACCESS = 0x001F0FFF,
            PROCESS_CREATE_PROCESS = 0x00000080,
            PROCESS_CREATE_THREAD = 0x00000002,
            PROCESS_DUP_HANDLE = 0x00000040,
            PROCESS_QUERY_INFORMATION = 0x00000400,
            PROCESS_QUERY_LIMITED_INFORMATION = 0x00001000,
            PROCESS_SET_INFORMATION = 0x00000200,
            PROCESS_SET_QUOTA = 0x00000100,
            PROCESS_SUSPEND_RESUME = 0x00000800,
            PROCESS_TERMINATE = 0x00000001,
            PROCESS_VM_OPERATION = 0x00000008,
            PROCESS_VM_READ = 0x00000010,
            PROCESS_VM_WRITE = 0x00000020
        }
        private static string GetExecutablePath(Process Process)
        {
            //If running on Vista or later use the new function
            if (Environment.OSVersion.Version.Major >= 6)
            {
                return GetExecutablePathAboveVista(Process.Id);
            }

            return Process.MainModule.FileName;
        }



        private static string GetExecutablePathAboveVista(int ProcessId)
        {
            var buffer = new StringBuilder(1024);
            
            IntPtr hprocess = OpenProcess(ProcessAccessFlags.PROCESS_QUERY_LIMITED_INFORMATION, false, ProcessId);

            if (hprocess != IntPtr.Zero)
            {
                try
                {
                    int size = buffer.Capacity;
                    if (QueryFullProcessImageName(hprocess, 0, buffer, out size))
                    {
                        return buffer.ToString();
                    }
                }
                finally
                {
                    CloseHandle(hprocess);
                }
            }

            return "Unknown";
            //throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        [DllImport("kernel32.dll")]
        private static extern bool QueryFullProcessImageName(IntPtr hprocess, int dwFlags,
                       StringBuilder lpExeName, out int size);
        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess,
                       bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hHandle);
        #endregion
    }
}
