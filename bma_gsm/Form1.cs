using System;
using System.Data;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;

namespace bma_gsm
{
    public partial class Form1 : Form
    {
        private SerialPort serialPort;
        private HttpListener httpListener;
        private Thread listenerThread;
        private DataTable dataTable;
        private System.Windows.Forms.Timer refreshTimer;
        public Form1()
        {
            InitializeComponent();

            // Initialize SerialPort
            serialPort = new SerialPort();
            serialPort.BaudRate = 9600; // Change this to your baud rate
            serialPort.DataReceived += SerialPort_DataReceived;

            // Populate ComboBox with available ports
            string[] ports = SerialPort.GetPortNames();
            PortName.Items.AddRange(ports);

            if (ports.Length > 0)
            {
                // Set default port
                PortName.SelectedIndex = 0;
                serialPort.PortName = ports[0];
            }

            refreshTimer = new System.Windows.Forms.Timer();
            refreshTimer.Interval = 5000;
            refreshTimer.Tick += new EventHandler(timer1_Tick); 
            refreshTimer.Start();
            StartListener();

            DataGridViewButtonColumn buttonColumn = new DataGridViewButtonColumn();
            buttonColumn.Name = "Properties";
            buttonColumn.Text = "Resend";
            buttonColumn.UseColumnTextForButtonValue = true;
            dataGridView1.Columns.Add(buttonColumn);

            dataTable = new DataTable();
            dataTable.Columns.Add("Host", typeof(string));
            dataTable.Columns.Add("Client Number", typeof(string));
            dataTable.Columns.Add("Status", typeof(string));
            dataGridView1.DataSource = dataTable;
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            // This event will be called when data is received
            SerialPort sp = (SerialPort)sender;
            string data = sp.ReadExisting(); // Read the available data

            // Update the ComboBox with received data
            Invoke(new Action(() =>
            {
                PortName.Items.Add(data);
            }));
        }

        private void StartListener()
        {
            try
            {
                httpListener = new HttpListener();
                httpListener.Prefixes.Add("http://localhost:9090/");//ito ung host
                httpListener.Start();

                // Start a new thread to handle incoming requests
                listenerThread = new Thread(HandleRequests);
                listenerThread.Start();
                LogMessage("Server started.");
            }
            catch (Exception ex)
            {
                LogMessage($"Error starting server: {ex.Message}");
            }
        }

        private void HandleRequests()
        {
            while (httpListener.IsListening)
            {
                try
                {
                    HttpListenerContext context = httpListener.GetContext();
                    ProcessRequest(context);
                }
                catch (Exception ex)
                {
                    LogMessage($"Error handling request: {ex.Message}");
                }
            }
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            try
            {
                HttpListenerRequest request = context.Request;
                string requestData = new System.IO.StreamReader(request.InputStream, request.ContentEncoding).ReadToEnd();

                DisplayRequestInRichTextBox(requestData);

                HttpListenerResponse response = context.Response;
                string responseString = "<html><body><h1>Hello from LocalHost!</h1></body></html>";
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);

                response.ContentType = "text/html";
                response.ContentLength64 = buffer.Length;

                System.IO.Stream output = response.OutputStream;
                output.Write(buffer, 0, buffer.Length);
                output.Close();
            }
            catch (Exception ex)
            {
                LogMessage($"Error processing request: {ex.Message}");
            }
        }
        private void DisplayRequestInRichTextBox(string requestData)
        {
            string[] requestDataParts = requestData.Split(',');

            // Remove specific characters from each element of the array
            for (int i = 0; i < requestDataParts.Length; i++)
            {
                requestDataParts[i] = requestDataParts[i].Replace("\"", "").Replace("[", "").Replace("]", "");
            }

            if (requestDataParts.Length >= 2) // kung ilang data pinapasa
            {
                DataRow newRow = dataTable.NewRow();
                newRow["Host"] = requestDataParts[0]; 
                newRow["Client Number"] = requestDataParts[1];

                if (requestDataParts[1] == "" || PortName.Text == "")
                {
                    MessageBox.Show("Please Connect Port");
                }
                else
                {
                    SerialPort sp = new SerialPort();
                    sp.PortName = PortName.Text;
                    sp.Open();
                    sp.WriteLine("AT" + Environment.NewLine);
                    Thread.Sleep(100);
                    sp.WriteLine("AT+CMGF=1" + Environment.NewLine);
                    Thread.Sleep(100);
                    sp.WriteLine("AT+CSCS=\"GSM\"" + Environment.NewLine);
                    Thread.Sleep(100);
                    sp.WriteLine("AT+CMGF\"" + requestDataParts[1] + "\"" + Environment.NewLine);
                    Thread.Sleep(100);
                    sp.WriteLine("SMS MESSAGE FROM BALIWAG MARITIME ACADEMY" + Environment.NewLine);
                    Thread.Sleep(100);
                    sp.Write(new byte[] { 26 }, 0, 1);
                    Thread.Sleep(100);
                    var response = sp.ReadExisting();
                    if (response.Contains("ERROR"))
                    {
                        newRow["Status"] = "Failed";
                    }
                    else
                    {
                        newRow["Status"] = "Success";
                    }
                    sp.Close();
                }
                dataTable.Rows.Add(newRow);
            }
            if (dataGridView1.InvokeRequired)
            {
                dataGridView1.Invoke((MethodInvoker)delegate {
                    dataGridView1.DataSource = null; 
                    dataGridView1.DataSource = dataTable; 
                });
            }
            else
            {
                dataGridView1.DataSource = null; 
                dataGridView1.DataSource = dataTable; 
            }
        }
        private void LogMessage(string message)
        {
            if (server_log.InvokeRequired)
            {
                server_log.Invoke((MethodInvoker)delegate { server_log.AppendText($"{message}\n"); });
            }
            else
            {
                server_log.AppendText($"{message}\n");
            }
        }

        private async Task ReceiveDataFromServer()
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    HttpResponseMessage response = await httpClient.GetAsync("http://127.0.0.1:8000/connection"); //ito ung laravel connection
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync();
                    websites.Text = responseBody; // ito ung ilang naka connect bali kung mag add another add getAsync
                    //response then "  int connections = Convert.ToInt32(responseBody) + Convert.ToInt32(responseBody);
                    //websites.Text = Convert.ToString(connections);
                }
            }
            catch (HttpRequestException ex)
            {
                websites.Text = "0"; //pag alang nakaconnect then 0 connection
                server_log.Text = Convert.ToString(ex); //kung my error naganap papakita sa serverlog
            }
        }
        private void button1_Click(object sender, EventArgs e)
        {
            httpListener?.Stop();
            httpListener?.Close();
            StartListener();
        }

        private async void timer1_Tick(object sender, EventArgs e)
        {
            await ReceiveDataFromServer(); //timer every 5sec tinitignan kung my nakaconnect sa laravel
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
            // Connect to the selected port
            if (PortName.Text == "")
            {
                MessageBox.Show("Select PortName");
            }
            else
            {
                if (!serialPort.IsOpen)
                {
                    serialPort.PortName = PortName.SelectedItem.ToString();

                    try
                    {
                        serialPort.Open();
                        connectPort.Enabled = false; // Disable the connect button after successful connection
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error opening serial port: " + ex.Message);
                    }
                }
            }
        }
    }
}
