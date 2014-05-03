using System;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SpeechRecognizer
{
    /// <summary>
    /// Connects to an RTP stream and listens for data
    /// </summary>
    public class UDPClient
    {
        private const int AUDIO_BUFFER_SIZE = 65536;

        private UdpClient client;
        private IPEndPoint endPoint;
        private SpeechStreamer audioStream;
        private bool writeHeaderToConsole = false;
        private bool listening = false;
        private int port;
        private Thread listenerThread;
        private Task<UdpReceiveResult> currentReceive;

        /// <summary>
        /// Returns a reference to the audio stream
        /// </summary>
        public SpeechStreamer AudioStream
        {
            get { return audioStream; }
        }
        /// <summary>
        /// Gets whether the client is listening for packets
        /// </summary>
        public bool Listening
        {
            get { return listening; }
        }
        /// <summary>
        /// Gets the port the RTP client is listening on
        /// </summary>
        public int Port
        {
            get { return port; }
        }

        /// <summary>
        /// RTP Client for receiving an RTP stream containing a WAVE audio stream
        /// </summary>
        /// <param name="port">The port to listen on</param>
        public UDPClient(int port)
        {
            Console.WriteLine(" [UDPClient] Loading...");

            this.port = port;

            // Initialize the audio stream that will hold the data
            audioStream = new SpeechStreamer(AUDIO_BUFFER_SIZE);

            Console.WriteLine(" Done");
        }

        /// <summary>
        /// Creates a connection to the RTP stream
        /// </summary>
        public void StartClient()
        {
            // Create new UDP client. The IP end point tells us which IP is sending the data
            client = new UdpClient(port);

            listening = true;
            listenerThread = new Thread(ReceiveCallback);
            listenerThread.Start();

            Console.WriteLine(" [UDPClient] Listening for packets on port " + port + "...");
        }

        /// <summary>
        /// Tells the UDP client to stop listening for packets.
        /// </summary>
        public void StopClient()
        {
            // Set the boolean to false to stop the asynchronous packet receiving
            listening = false;
            client.Close();
            audioStream.Close();
            //client.EndReceive(currentReceive, ref endPoint);
            Console.WriteLine(" [UDPClient] Stopped listening on port " + port);
        }

        /// <summary>
        /// Handles the receiving of UDP packets from the RTP stream
        /// </summary>
        /// <param name="ar">Contains packet data</param>
        private void ReceiveCallback()
        {
            // Receive packet
            currentReceive = client.ReceiveAsync();
            currentReceive.ContinueWith((task) =>
            {
                if (!task.IsCanceled && !task.IsFaulted)
                {
                    var data = task.Result.Buffer;
                    // Write the packet to the audio stream
                    audioStream.Write(data, 0, data.Length);
                    if (listening)
                    {
                        ReceiveCallback();
                    }
                }
            });
        }
    }
}
