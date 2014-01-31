using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Recognition;
using System.Text;
using System.Threading.Tasks;

namespace SpeechRecognizer
{
    class Microphone
    {
        private SpeechRecognitionEngine sre;
        private string status;
        private bool shouldBeOn;
        private int port;

        public Microphone(SpeechRecognitionEngine sre, string status, bool shouldBeOn, int port)
        {
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
    }
}
