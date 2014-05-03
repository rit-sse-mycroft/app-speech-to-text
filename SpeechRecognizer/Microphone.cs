using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Recognition;
using System.Text;
using System.Threading.Tasks;

namespace SpeechRecognizer
{
    /// <summary>
    /// Class representing a Microphone instance
    /// </summary>
    class Microphone
    {
        private SpeechRecognitionEngine sre;
        private string status;
        private bool shouldBeOn;
        private int port;
        private UDPClient client;

        /// <summary>
        /// Constructor for a Microphone
        /// </summary>
        /// <param name="sre">The speech recognition engine associate with this microphone</param>
        /// <param name="status">The status of the microphone</param>
        /// <param name="shouldBeOn">Should the speech recognition engine for this microphone be on</param>
        /// <param name="port">The por this microphone is asociated with</param>
        public Microphone(SpeechRecognitionEngine sre, UDPClient client, string status, bool shouldBeOn, int port)
        {
            this.client = client;
            this.sre = sre;
            this.status = status;
            this.port = port;
        }

        public SpeechRecognitionEngine Sre
        {
            get { return sre; }
        }

        public string Status
        {
            get { return status; }
            set { status = value; }
        }

        public bool ShouldBeOn
        {
            get { return shouldBeOn; }
        }
        
        public int Port
        {
             get { return port; }
        }

        public UDPClient Client
        {
            get { return client; }
        }
    }
}
