using System;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;

namespace PixartMouseDriver
{
    public partial class Form1 : Form
    {

        public enum StreamState
        {
            STARTING, RUNNING, STOPPING, STOPPED, ERROR
        }
        public StreamState state;
        public int port;

        public Form1()
        {
            InitializeComponent();
            port = 4507;
            state = StreamState.STOPPED;
        }

        private void updateState()
        {
            statusLabel.Text = state.ToString();
        }

        private void startButton_Click(object sender, EventArgs e)
        {
            switch (state)
            {
                case StreamState.STOPPED:
                case StreamState.ERROR:
                    state = StreamState.STARTING;
                    this.updateState();
                    backgroundWorker1.RunWorkerAsync(hostnameInput.Text);
                    break;
                case StreamState.STARTING:
                case StreamState.RUNNING:
                case StreamState.STOPPING:
                    break;
            }
        }

        private void stopButton_Click(object sender, EventArgs e)
        {
            switch (state)
            {
                case StreamState.STOPPED:
                case StreamState.ERROR:
                case StreamState.STOPPING:
                    break;
                case StreamState.STARTING:
                case StreamState.RUNNING:
                    state = StreamState.STOPPING;
                    this.updateState();
                    break;
            }
        }

        private void startStream(string hostname, string destination)
        {
            string postData = "ip=" + destination;
            postData += "&port=" + this.port;
            // Start streaming to this port
            NetworkOperations.sendRequestToEndpoint("http://" + hostname + "/stop", postData);
            Thread.Sleep(200);
            NetworkOperations.sendRequestToEndpoint("http://" + hostname + "/start", postData);
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            // Build parameters
            string hostname = (string)e.Argument;
            string nextHop = NetworkOperations.GetIPToHost(hostname);
            if (nextHop == null)
            {
                state = StreamState.ERROR;
                return;
            }
            startStream(hostname, nextHop);

            switch (state)
            {
                case StreamState.STOPPED:
                case StreamState.ERROR:
                case StreamState.RUNNING:
                case StreamState.STOPPING:
                    return;
                case StreamState.STARTING:
                    state = StreamState.RUNNING;
                    backgroundWorker1.ReportProgress(0);
                    break;
            }

            // Start receiving from this port
            using (UdpClient client = new UdpClient(port))
            {
                client.Client.ReceiveTimeout = 500;
                IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

                MouseDriver driver = new MouseDriver();
                try
                {
                    while (state == StreamState.RUNNING)
                    {
                        // Receive UDP packet
                        Byte[] receivedResults = client.Receive(ref remoteEndPoint);
                        string result = Encoding.ASCII.GetString(receivedResults);
                        Debug.Print("Packet: {0}", result);

                        // Parse packet into integers
                        char[] delimiterChars = { ' ', ',', '[', ']', '\r', '\n' };
                        string[] entries = result.Split(delimiterChars);
                        int[] coordinates = entries.Where(s => s.Length > 0).Select(s => Int32.Parse(s)).ToArray();

                        // Reject malformed packets
                        if (coordinates.Length != 13)
                        {
                            break;
                        }

                        // Handle packet
                        driver.handlePacket(coordinates);
                    }
                    state = StreamState.STOPPED;
                }
                catch (Exception exp)
                {
                    Debug.Print("Exception: {0}", exp.Message);
                    state = StreamState.ERROR;
                }
            }
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            this.updateState();
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.updateState();
        }

        private void scanButton_Click(object sender, EventArgs e)
        {
            if (!backgroundWorker2.IsBusy)
            {
                backgroundWorker2.RunWorkerAsync();
            }
        }

        private void backgroundWorker2_DoWork(object sender, DoWorkEventArgs e)
        {
            e.Result = NetworkOperations.getHostListeningOnPort(port);
        }

        private void backgroundWorker2_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result != null)
            {
                hostnameInput.Text = (string)e.Result;
            }
        }
    }
}
