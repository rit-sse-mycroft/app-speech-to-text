using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.Net;
using System.Net.Sockets;
using Microsoft.CSharp.RuntimeBinder;
using System.Speech.Recognition;
using System.Globalization;
using System.Speech.Recognition.SrgsGrammar;
using System.Xml;
using System.Speech.AudioFormat;
using Mycroft.App.Message;

namespace SpeechRecognizer
{
    class CombinedGrammar 
    {
        public string name;
        public string xml;
        public SrgsDocument srgsdoc;
        public Grammar compiled;
        public CombinedGrammar(string name, string xml) 
        {
            this.name = name;
            this.xml = xml;
            var xmlread = XmlReader.Create(new MemoryStream(Encoding.UTF8.GetBytes(xml)));
            srgsdoc = new SrgsDocument(xmlread);
            compiled = new Grammar(srgsdoc);
            compiled.Name = name;
        }
    }

    class NegotiatedAudioStream
    {
        public AudioBitsPerSample bps;
        public AudioChannel channels;
        public int rate;
        public Stream input;

        public NegotiatedAudioStream(Stream stream, int rate, AudioBitsPerSample bps, AudioChannel channels)
        {
            this.rate = rate;
            this.bps = bps;
            this.channels = channels;
            this.input = stream;
        }
    }

    class UdpState
    {
        public UdpClient client;
        public IPEndPoint endPoint;
        public MemoryStream stream;

        public UdpState(UdpClient client, IPEndPoint endPoint, MemoryStream stream)
        {
            this.client = client;
            this.endPoint = endPoint;
            this.stream = stream;
        }
    }
    
    public class SpeechServer : Mycroft.App.Server
    {

        private Dictionary<string, SpeechRecognitionEngine> sres;
        private Dictionary<string, CombinedGrammar> grammars;
        private string ipAddress;
        private int port;
  

        public SpeechServer() : base()
        {
            sres = new Dictionary<string, SpeechRecognitionEngine>();
            grammars = new Dictionary<string, CombinedGrammar>();
            WebRequest request = WebRequest.Create("http://checkip.dyndns.org/");
            using (WebResponse response = request.GetResponse())
            using (StreamReader stream = new StreamReader(response.GetResponseStream()))
            {
                ipAddress = stream.ReadToEnd();
            }

            //Search for the ip in the html
            int first = ipAddress.IndexOf("Address: ") + 9;
            int last = ipAddress.LastIndexOf("</body>");
            ipAddress = ipAddress.Substring(first, last - first);
            port = 1848;
        }

        
        public void AddGrammar(string name, string xml)
        {
            var gram = new CombinedGrammar(name, xml);
            grammars.Add(name, gram);
            foreach (var kv in sres)
            {
                Console.WriteLine("Here");
                kv.Value.RecognizeAsyncStop();
                kv.Value.LoadGrammarAsync(gram.compiled);
                kv.Value.RecognizeAsync(RecognizeMode.Multiple);
            }
        }

        public void RemoveGrammar(string name)
        {
            foreach (var kv in sres)
            {
                kv.Value.UnloadGrammar(grammars[name].compiled);
            }
            grammars.Remove(name);
        }

        private void RecognitionHandler(object sender, SpeechRecognizedEventArgs arg)
        {
            var text = arg.Result.Text;
            var tags = arg.Result.Semantics;
            var obj = new { content = new { text = text, tags = tags, grammar = arg.Result.Grammar.Name } };

            SendJson("MSG_BROADCAST", obj);
        }

        static void SpeechRecognitionRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            Console.WriteLine("Speech input was rejected.");
            foreach (RecognizedPhrase phrase in e.Result.Alternates)
            {
                Console.WriteLine("  Rejected phrase: " + phrase.Text);
                Console.WriteLine("  Confidence score: " + phrase.Confidence);
            }
        }

        public void AddInputMic(string instance, Stream stream)
        {
            try 
            {
                var negotiated = NegotiateAudioStream(instance, stream); //Throws IOException if connection cannot be made
                var sre = new SpeechRecognitionEngine(new CultureInfo("en-US"));
                //sre.SetInputToDefaultAudioDevice();
                sre.SetInputToAudioStream(negotiated.input, new SpeechAudioFormatInfo(negotiated.rate, negotiated.bps, negotiated.channels));
                //sre.SetInputToWaveStream(negotiated.input);
                sre.SpeechRecognized += new EventHandler<SpeechRecognizedEventArgs>(RecognitionHandler);
                sre.SpeechRecognitionRejected += new EventHandler<SpeechRecognitionRejectedEventArgs>(SpeechRecognitionRejected);
                foreach (var g in grammars)
                {
                    sre.LoadGrammarAsync(g.Value.compiled);
                }
                sres.Add(instance, sre);
            }
            catch (IOException) 
            {
                //negotiating connection with mic failed.
            }
        }

        public void RemoveInputMic(string instance)
        {
            if (sres.ContainsKey(instance))
            {
                sres[instance].Dispose();
                sres.Remove(instance);
            }
        }

        private NegotiatedAudioStream NegotiateAudioStream(string instance, Stream stream)
        {
            return new NegotiatedAudioStream(stream, 44100, AudioBitsPerSample.Eight, AudioChannel.Stereo);
        }

        
        protected override void Response(APP_MANIFEST_OK type, dynamic message)  
        {
            InstanceId = message["instanceId"];
            Console.WriteLine("Recieved: " + type);
            return;
        }
        protected async override void Response(APP_DEPENDENCY _, dynamic message)  
        {
            try
            {
                dynamic mics = message.microphone;
                var ups =  new List<string>();
                foreach (FieldInfo fieldinfo in mics.GetType().GetFields())
                {
                    string inst = fieldinfo.Name;
                    string stat = (string)fieldinfo.GetValue(mics);
                    if (stat == "up")
                    {
                        ups.Add(inst);
                    }
                }
                if (ups.Count > 0)
                {
                    Up(ups);
                }
                else
                {
                    Down();
                }
            }
            catch (RuntimeBinderException)
            {
                Down();
            }
        }
            
        protected async override void Response(MSG_QUERY _, dynamic message)  
        {
            switch ((string)message["action"])
            {
                case "load_grammar":
                    {
                        var grammar = message["data"]["grammar"];
                        string name = grammar["name"];
                        string xml = grammar["xml"];
                        AddGrammar(name, xml);
                        Console.WriteLine("Added Grammar " + name);

                        await SendJson("MSG_QUERY_SUCCESS", new { id = message["id"], ret = new { } });
                        break;
                    }
                case "request_stt":
                    {
                        await SendJson("MSG_QUERY_SUCCESS", new { id = message["id"], ret = new {ip = ipAddress, port = port}});

                        MemoryStream ms = new MemoryStream();
                        IPEndPoint ip = new IPEndPoint(IPAddress.Parse("50.30.232.247"), port);
                        UdpClient client = new UdpClient(ip);
                        UdpState state = new UdpState(client, ip, ms);
                        client.BeginReceive(new AsyncCallback(DataReceived), state);

                        AddInputMic(message["id"], ms);
                        port++;
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
        }
        private static void DataReceived(IAsyncResult ar)
        {
            UdpClient client = ((UdpState)ar.AsyncState).client;
            IPEndPoint ipe = ((UdpState)ar.AsyncState).endPoint;
            MemoryStream ms = ((UdpState)ar.AsyncState).stream;
            Byte[] receivedBytes = client.EndReceive(ar, ref ipe);
           // Console.WriteLine(Encoding.Default.GetString(receivedBytes));
            ms.Write(receivedBytes, 0, receivedBytes.Length);
            client.BeginReceive(new AsyncCallback(DataReceived), ar.AsyncState);
        }
         

        protected async override void Response(MSG_BROADCAST _, dynamic message)
        {
            try
            {
                RemoveGrammar(message.content.unloadGrammar);
                var obj = new {message = "Success"};
                await SendJson("MSG_BROADCAST_SUCCESS", obj);
            }
            catch (RuntimeBinderException)
            {
                //Didn't have a grammar removal request
            }

            return;
        }

        public void Up(List<string> instances)
        {
            //Connect to mic instances, hook up their streams to us
            foreach (var name in instances)
            {
                //AddInputMic(name);
            }
        }

        public void Down()
        {
            //Stop listening, no input sources available.
            foreach (var kvs in sres)
            {
                RemoveInputMic(kvs.Key);
            }
        }

    }
}
