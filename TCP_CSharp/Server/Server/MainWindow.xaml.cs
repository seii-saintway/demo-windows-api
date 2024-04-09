using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Server
{
    public partial class MainWindow : Window
    {
        private bool active = false;
        private Thread listener = null;
        private long id = 0;
        private struct MyClient
        {
            public long id;
            public StringBuilder username;
            public TcpClient client;
            public NetworkStream stream;
            public byte[] buffer;
            public StringBuilder data;
            public EventWaitHandle handle;
        };
        private ConcurrentDictionary<long, MyClient> clients = new ConcurrentDictionary<long, MyClient>();
        private Task send = null;
        private Thread disconnect = null;
        private bool exit = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Log(string msg = "")
        {
            logTextBox.Dispatcher.Invoke(() =>
            {
                if (!string.IsNullOrEmpty(msg))
                {
                    logTextBox.AppendText($"[ {DateTime.Now.ToString("HH:mm")} ] {msg}{Environment.NewLine}");
                }
                else
                {
                    logTextBox.Clear();
                }
            });
        }

        private string ErrorMsg(string msg)
        {
            return string.Format("ERROR: {0}", msg);
        }

        private string SystemMsg(string msg)
        {
            return string.Format("SYSTEM: {0}", msg);
        }

        private void Active(bool status)
        {
            startButton.Dispatcher.Invoke(() =>
            {
                active = status;
                if (status)
                {
                    addrTextBox.IsEnabled = false;
                    portTextBox.IsEnabled = false;
                    usernameTextBox.IsEnabled = false;
                    keyTextBox.IsEnabled = false;
                    startButton.Content = "Stop";
                    Log("Server has started");
                }
                else
                {
                    addrTextBox.IsEnabled = true;
                    portTextBox.IsEnabled = true;
                    usernameTextBox.IsEnabled = true;
                    keyTextBox.IsEnabled = true;
                    startButton.Content = "Start";
                    Log("Server has stopped");
                }
            });
        }

        private void AddToGrid(long id, string name)
        {
            if (!exit)
            {
                clientsDataGridView.Dispatcher.Invoke(() =>
                {
                    string[] row = new string[] { id.ToString(), name };
                    clientsDataGridView.Items.Add(row);
                    totalLabel.Content = string.Format("Total clients: {0}", clientsDataGridView.Items.Count);
                });
            }
        }

        private void RemoveFromGrid(long id)
        {
            if (!exit)
            {
                clientsDataGridView.Dispatcher.Invoke(() =>
                {
                    foreach (var item in clientsDataGridView.Items.Cast<string[]>())
                    {
                        if (item[0] == id.ToString())
                        {
                            clientsDataGridView.Items.Remove(item);
                            break;
                        }
                    }
                    totalLabel.Content = string.Format("Total clients: {0}", clientsDataGridView.Items.Count);
                });
            }
        }

        private void Read(IAsyncResult result)
        {
            MyClient obj = (MyClient)result.AsyncState;
            int bytes = 0;
            if (obj.client.Connected)
            {
                try
                {
                    bytes = obj.stream.EndRead(result);
                }
                catch (Exception ex)
                {
                    Log(ErrorMsg(ex.Message));
                    Log(ErrorMsg("Stack Trace: " + ex.StackTrace));
                }
            }
            if (bytes > 0)
            {
                obj.data.AppendFormat("{0}", Encoding.UTF8.GetString(obj.buffer, 0, bytes));
                try
                {
                    if (obj.stream.DataAvailable)
                    {
                        obj.stream.BeginRead(obj.buffer, 0, obj.buffer.Length, new AsyncCallback(Read), obj);
                    }
                    else
                    {
                        string msg = string.Format("{0}: {1}", obj.username, obj.data);
                        Log(msg);
                        Send(msg, obj.id);
                        obj.data.Clear();
                        obj.handle.Set();
                    }
                }
                catch (Exception ex)
                {
                    obj.data.Clear();
                    Log(ErrorMsg(ex.Message));
                    Log(ErrorMsg("Stack Trace: " + ex.StackTrace));
                    obj.handle.Set();
                }
            }
            else
            {
                obj.client.Close();
                obj.handle.Set();
            }
        }

        private void ReadAuth(IAsyncResult result)
        {
            MyClient obj = (MyClient)result.AsyncState;
            int bytes = 0;
            if (obj.client.Connected)
            {
                try
                {
                    bytes = obj.stream.EndRead(result);
                }
                catch (Exception ex)
                {
                    Log(ErrorMsg(ex.Message));
                    Log(ErrorMsg("Stack Trace: " + ex.StackTrace));
                }
            }
            if (bytes > 0)
            {
                obj.data.AppendFormat("{0}", Encoding.UTF8.GetString(obj.buffer, 0, bytes));
                try
                {
                    if (obj.stream.DataAvailable)
                    {
                        obj.stream.BeginRead(obj.buffer, 0, obj.buffer.Length, new AsyncCallback(ReadAuth), obj);
                    }
                    else
                    {
                        Dictionary<string, string> data = JsonSerializer.Deserialize<Dictionary<string, string>>(obj.data.ToString());

                        string keyText = string.Empty;
                        keyTextBox.Dispatcher.Invoke(() => {
                            keyText = keyTextBox.Text;
                        });

                        if (!data.ContainsKey("username") || data["username"].Length < 1 || !data.ContainsKey("key") || !data["key"].Equals(keyText)) {
                            obj.client.Close();
                        } else {
                            obj.username.Append(data["username"].Length > 200 ? data["username"].Substring(0, 200) : data["username"]);
                            Send("{\"status\": \"authorized\"}", obj);
                        }
                        obj.data.Clear();
                        obj.handle.Set();
                    }
                }
                catch (Exception ex)
                {
                    obj.data.Clear();
                    Log(ErrorMsg(ex.Message));
                    Log(ErrorMsg("Stack Trace: " + ex.StackTrace));
                    obj.handle.Set();
                }
            }
            else
            {
                obj.client.Close();
                obj.handle.Set();
            }
        }

        private bool Authorize(MyClient obj)
        {
            bool success = false;
            while (obj.client.Connected)
            {
                try
                {
                    obj.stream.BeginRead(obj.buffer, 0, obj.buffer.Length, new AsyncCallback(ReadAuth), obj);
                    obj.handle.WaitOne();
                    if (obj.username.Length > 0)
                    {
                        success = true;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Log(ErrorMsg(ex.Message));
                    Log(ErrorMsg("Stack Trace: " + ex.StackTrace));
                }
            }
            return success;
        }

        private void Connection(MyClient obj)
        {
            if (Authorize(obj))
            {
                clients.TryAdd(obj.id, obj);
                AddToGrid(obj.id, obj.username.ToString());
                string msg = string.Format("{0} has connected", obj.username);
                Log(SystemMsg(msg));
                Send(SystemMsg(msg), obj.id);
                while (obj.client.Connected)
                {
                    try
                    {
                        obj.stream.BeginRead(obj.buffer, 0, obj.buffer.Length, new AsyncCallback(Read), obj);
                        obj.handle.WaitOne();
                    }
                    catch (Exception ex)
                    {
                        Log(ErrorMsg(ex.Message));
                        Log(ErrorMsg("Stack Trace: " + ex.StackTrace));
                    }
                }
                obj.client.Close();
                clients.TryRemove(obj.id, out MyClient tmp);
                RemoveFromGrid(tmp.id);
                msg = string.Format("{0} has disconnected", tmp.username);
                Log(SystemMsg(msg));
                Send(msg, tmp.id);
            }
        }

        private void Listener(IPAddress ip, int port)
        {
            TcpListener listener = null;
            try
            {
                listener = new TcpListener(ip, port);
                listener.Start();
                Active(true);
                while (active)
                {
                    if (listener.Pending())
                    {
                        try
                        {
                            MyClient obj = new MyClient();
                            obj.id = id;
                            obj.username = new StringBuilder();
                            obj.client = listener.AcceptTcpClient();
                            obj.stream = obj.client.GetStream();
                            obj.buffer = new byte[obj.client.ReceiveBufferSize];
                            obj.data = new StringBuilder();
                            obj.handle = new EventWaitHandle(false, EventResetMode.AutoReset);
                            Thread th = new Thread(() => Connection(obj))
                            {
                                IsBackground = true
                            };
                            th.Start();
                            ++id;
                        }
                        catch (Exception ex)
                        {
                            Log(ErrorMsg(ex.Message));
                            Log(ErrorMsg("Stack Trace: " + ex.StackTrace));
                        }
                    }
                    else
                    {
                        Thread.Sleep(500);
                    }
                }
                Active(false);
            }
            catch (Exception ex)
            {
                Log($"ERROR: {ex.Message}");
            }
            finally
            {
                if (listener != null)
                {
                    listener.Server.Close();
                }
            }
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            if (active) {
                active = false;
            } else if (listener == null || !listener.IsAlive) {
                string address = addrTextBox.Text.Trim();
                string number = portTextBox.Text.Trim();
                string username = usernameTextBox.Text.Trim();
                bool error = false;
                IPAddress ip = null;
                if (address.Length < 1) {
                    error = true;
                    Log(SystemMsg("Address is required"));
                } else {
                    try {
                        ip = IPAddress.Parse(address);
                    } catch {
                        error = true;
                        Log(SystemMsg("Address is not valid"));
                    }
                }
                int port = -1;
                if (number.Length < 1) {
                    error = true;
                    Log(SystemMsg("Port number is required"));
                } else if (!int.TryParse(number, out port)) {
                    error = true;
                    Log(SystemMsg("Port number is not valid"));
                } else if (port < 0 || port > 65535) {
                    error = true;
                    Log(SystemMsg("Port number is out of range"));
                }
                if (username.Length < 1) {
                    error = true;
                    Log(SystemMsg("Username is required"));
                }
                if (!error) {
                    listener = new Thread(() => Listener(ip, port))
                    {
                        IsBackground = true
                    };
                    listener.Start();
                }
            }
        }

        private void Write(IAsyncResult result)
        {
            MyClient obj = (MyClient)result.AsyncState;
            if (obj.client.Connected)
            {
                try
                {
                    obj.stream.EndWrite(result);
                }
                catch (Exception ex)
                {
                    Log(ErrorMsg(ex.Message));
                    Log(ErrorMsg("Stack Trace: " + ex.StackTrace));
                }
            }
        }

        private void BeginWrite(string msg, MyClient obj) // send the message to a specific client
        {
            byte[] buffer = Encoding.UTF8.GetBytes(msg);
            if (obj.client.Connected)
            {
                try
                {
                    obj.stream.BeginWrite(buffer, 0, buffer.Length, new AsyncCallback(Write), obj);
                }
                catch (Exception ex)
                {
                    Log(ErrorMsg(ex.Message));
                    Log(ErrorMsg("Stack Trace: " + ex.StackTrace));
                }
            }
        }

        private void BeginWrite(string msg, long id = -1) // send the message to everyone except the sender or set ID to lesser than zero to send to everyone
        {
            byte[] buffer = Encoding.UTF8.GetBytes(msg);
            foreach (KeyValuePair<long, MyClient> obj in clients)
            {
                if (id != obj.Value.id && obj.Value.client.Connected)
                {
                    try
                    {
                        obj.Value.stream.BeginWrite(buffer, 0, buffer.Length, new AsyncCallback(Write), obj.Value);
                    }
                    catch (Exception ex)
                    {
                        Log(ErrorMsg(ex.Message));
                        Log(ErrorMsg("Stack Trace: " + ex.StackTrace));
                    }
                }
            }
        }

        private void Send(string msg, MyClient obj)
        {
            if (send == null || send.IsCompleted)
            {
                send = Task.Factory.StartNew(() => BeginWrite(msg, obj));
            }
            else
            {
                send.ContinueWith(antecendent => BeginWrite(msg, obj));
            }
        }

        private void Send(string msg, long id = -1)
        {
            if (send == null || send.IsCompleted)
            {
                send = Task.Factory.StartNew(() => BeginWrite(msg, id));
            }
            else
            {
                send.ContinueWith(antecendent => BeginWrite(msg, id));
            }
        }

        private void SendTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                if (sendTextBox.Text.Length > 0)
                {
                    string msg = sendTextBox.Text;
                    sendTextBox.Clear();
                    Log($"{usernameTextBox.Text.Trim()} (You): {msg}");
                    Send($"{usernameTextBox.Text.Trim()}: {msg}");
                }
            }
        }

        private void Disconnect(long id = -1) // disconnect everyone if ID is not supplied or is lesser than zero
        {
            if (disconnect == null || !disconnect.IsAlive)
            {
                disconnect = new Thread(() =>
                {
                    if (id >= 0)
                    {
                        clients.TryGetValue(id, out MyClient obj);
                        obj.client.Close();
                        RemoveFromGrid(obj.id);
                    }
                    else
                    {
                        foreach (KeyValuePair<long, MyClient> obj in clients)
                        {
                            obj.Value.client.Close();
                            RemoveFromGrid(obj.Value.id);
                        }
                    }
                })
                {
                    IsBackground = true
                };
                disconnect.Start();
            }
        }

        private void DisconnectButton_Click(object sender, EventArgs e)
        {
            Disconnect();
        }

        private void ClearButton_Click(object sender, EventArgs e)
        {
            Log();
        }

        private void keyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 在这里添加事件处理程序的逻辑
        }
    }
}
