﻿using System;
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
    
    public class SpeechServer : Mycroft.App.Server
    {

        private Dictionary<string, SpeechRecognitionEngine> sres;
        private Dictionary<string, object> mics;
        private Dictionary<string, string> grammars;
        private Dictionary<SpeechRecognitionEngine, int> audioLevels;
        private string ipAddress;
        private int port;
  
  

        public SpeechServer() : base()
        {
            sres = new Dictionary<string, SpeechRecognitionEngine>();
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
        }

        
        public void AddGrammar(string name, string xml)
        {
            grammars.Add(name, xml);
            foreach (var kv in sres)
            {
                var gram = new CombinedGrammar(name, xml);
                SpeechRecognitionEngine sre = kv.Value;
                sre.RequestRecognizerUpdate();
                sre.LoadGrammarAsync(gram.compiled);
                if ((string) mics[kv.Key] == "up" && sre.AudioState == AudioState.Stopped)
                    sre.RecognizeAsync(RecognizeMode.Multiple);
            }
        }

        public void RemoveGrammar(string name)
        {
            foreach (var kv in sres)
            {
                Grammar grammar = null;
                foreach (var gram in kv.Value.Grammars)
                {
                    if (gram.Name == name)
                    {
                        grammar = gram;
                        break;
                    } 
                }
                try
                {
                    kv.Value.RequestRecognizerUpdate();
                    kv.Value.UnloadGrammar(grammar);
                }
                catch 
                {
                    Console.WriteLine("Grammar " + name + "not loaded.");
                }
            }
            grammars.Remove(name);
        }

        private async void RecognitionHandler(object sender, SpeechRecognizedEventArgs arg)
        {
            var text = arg.Result.Text;
            var semantics = arg.Result.Semantics;
            var tags = new Dictionary<string, string>();
            foreach (var kv in semantics)
            {
                tags.Add(kv.Key, (string) kv.Value.Value);
            }
            var obj = new { content = new { text = text, tags = tags, grammar = arg.Result.Grammar.Name } };

            await SendJson("MSG_BROADCAST", obj);
        }

        private void RecognitionRejectedHandler(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            Console.WriteLine("Speech input was rejected.");
            foreach (RecognizedPhrase phrase in e.Result.Alternates)
            {
                Console.WriteLine("  Rejected phrase: " + phrase.Text);
                Console.WriteLine("  Confidence score: " + phrase.Confidence);
            }
        }

        private void AudioLevelUpdatedHandler(object sender, AudioLevelUpdatedEventArgs e)
        {
            audioLevels[(SpeechRecognitionEngine)sender] = e.AudioLevel;
            Console.WriteLine("Audio Level: " + e.AudioLevel);
        }

        public void AddInputMic(string instance, Stream stream)
        {
            try 
            {
                var sre = new SpeechRecognitionEngine(new CultureInfo("en-US"));
                sre.SetInputToAudioStream(stream, new SpeechAudioFormatInfo(16000, AudioBitsPerSample.Sixteen, AudioChannel.Mono));
                sre.SpeechRecognized += new EventHandler<SpeechRecognizedEventArgs>(RecognitionHandler);
                sre.SpeechRecognitionRejected += new EventHandler<SpeechRecognitionRejectedEventArgs>(RecognitionRejectedHandler);
                sre.AudioLevelUpdated += new EventHandler<AudioLevelUpdatedEventArgs>(AudioLevelUpdatedHandler);
                sres.Add(instance, sre);
                audioLevels.Add(sre, 0);
                foreach (var g in grammars)
                {
                    var gram = new CombinedGrammar(g.Key, g.Value);
                    sre.LoadGrammarAsync(gram.compiled);
                }
                if (sre.Grammars.Count > 0)
                {
                    sre.RecognizeAsync(RecognizeMode.Multiple);
                }
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
        
        protected async override void Response(APP_MANIFEST_OK type, dynamic message)  
        {
            InstanceId = message["instanceId"];
            Console.WriteLine("Recieved: " + type);
            await SendJson("APP_UP", new {});
            return;
        }
        protected async override void Response(APP_DEPENDENCY _, dynamic message)  
        {
            try
            {
               mics = message["microphone"];

                foreach (var mic in mics)
                {
                    if ((string) mic.Value == "up")
                    {
                        if (sres.ContainsKey(mic.Key))
                        {
                            SpeechRecognitionEngine sre = sres[mic.Key];
                            if (sre.AudioState == AudioState.Stopped)
                                sre.RecognizeAsync(RecognizeMode.Multiple);
                        } 
                        else
                        {
                            await SendJson("MSG_QUERY", new { id = Guid.NewGuid(), capability = "microphone", action = "invite", instanceId = new string[1] { mic.Key }, priority = 30, data = new { ip = ipAddress, port = port } });
                            IPEndPoint ip = new IPEndPoint(IPAddress.Parse(ipAddress), port);
                            RTPClient client = new RTPClient(port);
                            client.StartClient();
                            AddInputMic(mic.Key, client.AudioStream);
                            port++;
                        }
                    }
                    else
                    {
                        if (sres.ContainsKey(mic.Key))
                        {
                            SpeechRecognitionEngine sre = sres[mic.Key];
                            if (sre.AudioState != AudioState.Stopped)
                                sre.RecognizeAsyncStop();
                        }
                    }
                }
            }
            catch (RuntimeBinderException)
            {
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
                default:
                    {
                        break;
                    }
            }
        }
         

        protected override void Response(MSG_BROADCAST _, dynamic message)
        {
            try
            {
                RemoveGrammar(message["content"]["unloadGrammar"]);
                Console.WriteLine("Removed Grammar " + message["content"]["unloadGrammar"]);
            }
            catch
            {
                //Didn't have a grammar removal request
            }

            return;
        }

    }
}
