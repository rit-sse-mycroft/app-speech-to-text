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
using Mycroft.App;

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
    
    /// <summary>
    /// The Speech to text class
    /// </summary>
    public class SpeechClient : Client
    {

        private Dictionary<string, Microphone> mics;
        private Dictionary<string, string> grammars;
        private Dictionary<SpeechRecognitionEngine, int> audioLevels;
        private string ipAddress;
        private int port;
  
  
        /// <summary>
        /// Constructor for Speech client
        /// </summary>
        /// <param name="manifest">path to the app manifest</param>
        public SpeechClient(string manifest) : base(manifest)
        {
            mics = new Dictionary<string, Microphone>();
            grammars = new Dictionary<string, string>();
            audioLevels = new Dictionary<SpeechRecognitionEngine, int>();
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
            handler.On("APP_MANIFEST_OK", AppManifestOk);
            handler.On("APP_DEPENDENCY", AppDependency);
            handler.On("MSG_QUERY", MsgQuery);
            handler.On("MSG_BROADCAST", MsgBroadcast);
        }

        #region Grammars
        /// <summary>
        /// Adds a new grammar to the SREs
        /// </summary>
        /// <param name="name">the name of the grammar</param>
        /// <param name="xml">the grammar xml string</param>
        public void AddGrammar(string name, string xml)
        {

            grammars.Add(name, xml);
            foreach (var kv in mics)
            {
                var gram = new CombinedGrammar(name, xml);
                Microphone mic = kv.Value;
                mic.Sre.RequestRecognizerUpdate();
                mic.Sre.LoadGrammarAsync(gram.compiled);
            }
        }

        /// <summary>
        /// Removes a grammar from the SREs
        /// </summary>
        /// <param name="name">the name of the grammar</param>
        public void RemoveGrammar(string name)
        {
            foreach (var kv in mics)
            {
                Grammar grammar = null;
                foreach (var gram in kv.Value.Sre.Grammars)
                {
                    if (gram.Name == name)
                    {
                        grammar = gram;
                        break;
                    } 
                }
                try
                {
                    kv.Value.Sre.RequestRecognizerUpdate();
                    kv.Value.Sre.UnloadGrammar(grammar);
                }
                catch 
                {
                    Console.WriteLine("Grammar " + name + "not loaded.");
                }
            }
            grammars.Remove(name);
        }
        #endregion
        #region Speech Recognition Handlers
        /// <summary>
        /// Called when speech is recognized
        /// </summary>
        /// <param name="sender">the sender</param>
        /// <param name="arg">the result</param>
        private async void RecognitionHandler(object sender, SpeechRecognizedEventArgs arg)
        {
            var text = arg.Result.Text;
            var semantics = arg.Result.Semantics;
            Console.WriteLine("Speech input was accepted.");
            Console.WriteLine("  Accepted Phrase: " + text);
            Console.WriteLine("  Confidence Score: " + arg.Result.Confidence);

            if (arg.Result.Grammar.Name != "dictation")
            {
                var tags = new Dictionary<string, string>();
                foreach (var kv in semantics)
                {
                    tags.Add(kv.Key, (string)kv.Value.Value);
                }
                var content = new { text = text, tags = tags, grammar = arg.Result.Grammar.Name };

                await Broadcast(content);
            }
        }

        /// <summary>
        /// Called when speech is rejected
        /// </summary>
        /// <param name="sender">the sender</param>
        /// <param name="e">the result</param>
        private void RecognitionRejectedHandler(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            Console.WriteLine("Speech input was rejected.");
            foreach (RecognizedPhrase phrase in e.Result.Alternates)
            {
                Console.WriteLine("  Rejected phrase: " + phrase.Text);
                Console.WriteLine("  Confidence score: " + phrase.Confidence);
            }
        }
        #endregion
        #region Mics
        /// <summary>
        /// Adds a new microphone instance
        /// </summary>
        /// <param name="instance">The instance id of the microphone</param>
        /// <param name="stream">The audio stream</param>
        /// <param name="status">The status of the microphone</param>
        /// <param name="shouldBeOn">Whether the speech recognition engine should be turned on</param>
        public void AddInputMic(string instance, Stream stream, string status, bool shouldBeOn)
        {
            try 
            {
                var sre = new SpeechRecognitionEngine(new CultureInfo("en-US"));
                sre.SetInputToAudioStream(stream, new SpeechAudioFormatInfo(16000, AudioBitsPerSample.Sixteen, AudioChannel.Mono));
                sre.SpeechRecognized += new EventHandler<SpeechRecognizedEventArgs>(RecognitionHandler);
                sre.SpeechRecognitionRejected += new EventHandler<SpeechRecognitionRejectedEventArgs>(RecognitionRejectedHandler);
                DictationGrammar customDictationGrammar  = new DictationGrammar("grammar:dictation");
                customDictationGrammar.Name = "dictation";
                customDictationGrammar.Enabled = true;
                sre.LoadGrammar(customDictationGrammar);
                mics.Add(instance, new Microphone(sre, status, shouldBeOn,port));
                foreach (var g in grammars)
                {
                    var gram = new CombinedGrammar(g.Key, g.Value);
                    sre.LoadGrammarAsync(gram.compiled);
                }
                if (shouldBeOn)
                {
                    sre.RecognizeAsync(RecognizeMode.Multiple);
                }
            }
            catch (IOException) 
            {
                //negotiating connection with mic failed.
            }
        }

        /// <summary>
        /// Removes a microphone 
        /// </summary>
        /// <param name="instance">The instance ID of a microphone</param>
        public void RemoveInputMic(string instance)
        {
            if (mics.ContainsKey(instance))
            {
                mics[instance].Sre.RecognizeAsyncStop();
                mics.Remove(instance);
            }
        }
        #endregion
        #region Message Handlers
        /// <summary>
        /// Called when APP_MANIFEST_OK is recieved
        /// </summary>
        /// <param name="message">The message recieved</param>
        protected async void AppManifestOk(dynamic message)  
        {
            InstanceId = message["instanceId"];
            await Up();
        }

        /// <summary>
        /// Called when APP_DEPENDENCY is recieved
        /// </summary>
        /// <param name="message">The message recieved</param>
        protected void AppDependency(dynamic message)  
        {
            if (message.ContainsKey("microphone"))
            {
                var dep = message["microphone"];
                DepenedencyHelper(dep, true);
            }
            if (message.ContainsKey("mock_microphone"))
            {
                var dep = message["mock_microphone"];
                DepenedencyHelper(dep, false);
            }

        }
        
        /// <summary>
        /// Called when MSG_QUERY is recieved
        /// </summary>
        /// <param name="message">The message recieved</param>
        protected async void MsgQuery(dynamic message)  
        {
            switch ((string)message["action"])
            {
                case "load_grammar":
                    {
                        var grammar = message["data"]["grammar"];
                        string name = grammar["name"];
                        string xml = grammar["xml"];
                        try
                        {
                            AddGrammar(name, xml);
                            Console.WriteLine("Added Grammar " + name);

                            await QuerySuccess(message["id"], new { });
                        }
                        catch(ArgumentException)
                        {
                            QueryFail(message["id"], "Grammar has already been added");
                            Console.WriteLine("Couldn't add the grammar.");
                        }
                        break;
                    }
                case "unload_grammar":
                    {
                        var grammar = message["data"]["grammar"];
                        RemoveGrammar(grammar);
                        Console.WriteLine("Removed Grammar " + grammar);
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
        }

        /// <summary>
        /// Called when MSG_BROADCAST is recieved
        /// </summary>
        /// <param name="message">The message recieved</param>
        protected void MsgBroadcast(dynamic message)
        {
            var content = message["content"];
            string from = message["fromInstanceId"];
            if (content.ContainsKey("spoken_text"))
            {
                SpeechRecognitionEngine sre = mics[from].Sre;
                sre.EmulateRecognize(content["spoken_text"]);
            }
        }

        /// <summary>
        /// Helper method for dependencies
        /// </summary>
        /// <param name="dep">The dependencies</param>
        /// <param name="shouldBeOn">Should the SRE associated with this microphone be turned on or not</param>
        private async void DepenedencyHelper(dynamic dep, bool shouldBeOn)
        {
            foreach (var mic in dep)
            {
                if ((string)mic.Value == "up")
                {
                    if (mics.ContainsKey(mic.Key))
                    {
                        SpeechRecognitionEngine sre = mics[mic.Key].Sre;
                        if (sre.AudioState == AudioState.Stopped && mics[mic.Key].ShouldBeOn)
                            sre.RecognizeAsync(RecognizeMode.Multiple);
                    }
                    else
                    {
                        IPEndPoint ip = new IPEndPoint(IPAddress.Parse(ipAddress), port);
                        RTPClient client = new RTPClient(port);
                        client.StartClient();
                        AddInputMic(mic.Key, client.AudioStream, mic.Value, shouldBeOn);
                        port++;
                    }
                    await Query("microphone", "invite", new { ip = ipAddress, port = mics[mic.Key].Port }, new string[1] { mic.Key });
                }
                else
                {
                    if (mics.ContainsKey(mic.Key))
                    {
                        SpeechRecognitionEngine sre = mics[mic.Key].Sre;
                        if (sre.AudioState != AudioState.Stopped && shouldBeOn)
                            sre.RecognizeAsyncStop();
                    }
                }
            }
        }
        #endregion
    }
}
