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

namespace SpeechRecognizer
{
    public class SpeechServer
    {
        private string manifest;
        private TcpClient cli;
        private NetworkStream stream;
        private StreamWriter writer;
        private Encoding enc = new UTF8Encoding(true, true);
        private DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(object));
        private StreamReader reader;

        public SpeechServer()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var textStreamReader = new StreamReader(assembly.GetManifestResourceStream("SpeechRecognizer.app_manifest.json"));
            manifest = textStreamReader.ReadToEnd();
        }

        public void connect(string hostname, string port)
        {
            cli = new TcpClient(hostname, Convert.ToInt32(port));
            setStream(cli.GetStream());
        }

        public void setStream(Stream s)
        {
            writer = new StreamWriter(s, enc);
            reader = new StreamReader(s, enc);
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
