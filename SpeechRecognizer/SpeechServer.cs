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
using System.Runtime.Serialization.Json;
using Microsoft.CSharp.RuntimeBinder;
using System.Speech.Recognition;
using System.Globalization;
using System.Speech.Recognition.SrgsGrammar;
using System.Xml;
using System.Speech.AudioFormat;

namespace SpeechRecognizer
{
    private class CombinedGrammar 
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

    private class NegotiatedAudioStream
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
    
    public class SpeechServer
    {
        private string manifest;
        private TcpClient cli;
        private NetworkStream stream;
        private StreamWriter writer;
        private Encoding enc = new UTF8Encoding(true, true);
        private DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(object));
        private StreamReader reader;

        private Dictionary<string, SpeechRecognitionEngine> sres;
        private Dictionary<string, CombinedGrammar> grammars;

        public SpeechServer()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var textStreamReader = new StreamReader(assembly.GetManifestResourceStream("SpeechRecognizer.app_manifest.json"));
            manifest = textStreamReader.ReadToEnd();

            sres = new Dictionary<string, SpeechRecognitionEngine>();
            grammars = new Dictionary<string, CombinedGrammar>();
        }

        public void connect(string hostname, string port)
        {
            cli = new TcpClient(hostname, Convert.ToInt32(port));
            setStream(cli.GetStream());
            startListening();
        }

        public void setStream(Stream s)
        {
            writer = new StreamWriter(s, enc);
            reader = new StreamReader(s, enc);
        }

        public void addGrammar(string name, string xml)
        {
            var gram = new CombinedGrammar(name, xml);
            grammars.Add(name, gram);
            foreach (var kv in sres)
            {
                kv.Value.LoadGrammarAsync(gram.compiled);
            }
        }

        public void removeGrammar(string name)
        {
            foreach (var kv in sres)
            {
                kv.Value.UnloadGrammar(grammars[name].compiled);
            }
            grammars.Remove(name);
        }

        private void recognitionHandler(object sender, SpeechRecognizedEventArgs arg)
        {
            var text = arg.Result.Text;
            var tags = arg.Result.Semantics;
            var obj = new { content = new { text = text, tags = tags, grammar = arg.Result.Grammar.Name } };

            sendJson("MSG_BROADCAST", obj);
        }

        public void addInputMic(string instance)
        {
            try 
            {
                var negotiated = negotiateAudioStream(instance); //Throws IOException if connection cannot be made
                var sre = new SpeechRecognitionEngine(new CultureInfo("en-US"));
                sre.SetInputToAudioStream(negotiated.input, new SpeechAudioFormatInfo(negotiated.rate, negotiated.bps, negotiated.channel));
                sre.SpeechRecognized += recognitionHandler;
                sres.Add(instance, sre);
            }
            catch (IOException) 
            {
                //negotiating connection with mic failed.
            }
        }

        public void removeInputMic(string instance)
        {
            if (sres.ContainsKey(instance))
            {
                sres[instance].Dispose();
                sres.Remove(instance);
            }
        }

        private NegotiatedAudioStream negotiateAudioStream(string instance)
        {
            throw new IOException("Failed to negoiate an audio stream with "+instance);
            return new NegotiatedAudioStream(new MemoryStream(), 44100, AudioBitsPerSample.Sixteen, AudioChannel.Mono);
        }

        private delegate void response_del(SpeechServer self, dynamic message);

        private Dictionary<string,response_del> responses = new Dictionary<string,response_del>{
            {"APP_MANIFEST_OK", (self, message) => 
                {
                    Console.WriteLine(message.instanceId);
                    return;
                }
            },
            {"APP_MANIFEST_FAIL", (self, message) => 
                {
                    return;
                }
            },
            {"APP_DEPENDENCY", (self, message) => 
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
                            self.Up(ups);
                        }
                        else
                        {
                            self.Down();
                        }
                    }
                    catch (RuntimeBinderException)
                    {
                        self.Down();
                    }
                }
            },
            { "MSG_QUERY", (self, message) => 
                {
                    return;
                }
            },
            { "MSG_QUERY_SUCCESS", (self, message) =>
                {
                    return;
                }
            },
            { "MSG_QUERY_FAIL", (self, message) =>
                {
                    return;
                }
            },
            { "MSG_BROADCAST", (self, message) =>
                {
                    return;
                }
            },
            { "MSG_BROADCAST_SUCCESS", (self, message) =>
                {
                    return;
                }
            },
            { "MSG_BROADCAST_FAIL", (self, message) =>
                {
                    return;
                }
            }
        };

        public async void startListening()
        {
            await sendManifest();
            while (true) {
                dynamic obj = await readJson();
                string type = obj.type;
                dynamic message = obj.message;

                if (responses.ContainsKey(type)) 
                {
                    responses[type](this, message);
                }
            }
        }

        public void Up(List<string> instances)
        {
            //Connect to mic instances, hook up their streams to us
        }

        public void Down()
        {
            //Stop listening, no input sources available.
        }

        public async void closeConnection()
        {
            await sendData("APP_DOWN","");
            cli.Close();
        }

        public async Task sendData(string type, string data)
        {
            string msg = type + " " + data;
            string composition = enc.GetBytes(msg).Length.ToString() + "\n" + msg;
            await writer.WriteLineAsync(composition);
            writer.Flush();
        }

        public async Task sendJson(string type, Object o)
        {
            Stream s = new MemoryStream();
            ser.WriteObject(s,o);
            var reads = new StreamReader(s, enc);
            string obj = reads.ReadToEnd();
            string msg = type + " " + obj;
            await writer.WriteLineAsync(enc.GetBytes(msg).Length.ToString() + "\n" + msg);
            writer.Flush();
        }

        public async Task sendManifest()
        {
            await sendData("APP_MANIFEST", manifest);
        }

        public async Task<Object> readJson()
        {
            //Size of message in bytes
            string len = reader.ReadLine();
            var size = Convert.ToInt32(len);
            
            //buffer to put message in
            var buf = new Char[size]; 

            //Get the message
            await reader.ReadBlockAsync(buf, 0, size);
            var str = Convert.ToString(buf);
            var re = new Regex(@"^([A-Z_]*)");

            //Match the message type
            var match = re.Match(str);
            if (match.Length <= 0)
            {
                throw new ArgumentException("Couldn't match a message type string in message: " + str);
            }

            //Convert the json string to an object
            var jsonstr = str.TrimStart(match.Value.ToCharArray());
            if (jsonstr.Trim().Length == 0)
            {
                return new { 
                    type = match.Value,
                    message = new { }
                };
            }
            var jsonmem = new MemoryStream(enc.GetBytes(jsonstr));
            var obj = ser.ReadObject(jsonmem);

            //Return the type string and the object
            return new {
                type = match.Value, 
                message = obj
            };
        }
    }
}
