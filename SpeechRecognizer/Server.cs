using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Mycroft.App.Message;
using System.Web.Script.Serialization;
using System.Diagnostics; //This can be used with dynamic, unlike the contract

namespace Mycroft.App
{
    public abstract class Server
    {
        private string manifest;
        private TcpClient cli;
        private Stream stream;
        private JavaScriptSerializer ser = new JavaScriptSerializer();
        private StreamReader reader;
        public string InstanceId;

        public Server()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var textStreamReader = new StreamReader(assembly.GetManifestResourceStream("SpeechRecognizer.app_manifest.json"));
            manifest = textStreamReader.ReadToEnd();
            var jsobj = ser.Deserialize<dynamic>(manifest);
            InstanceId = jsobj["instanceId"];

        }
        public async void Connect(string hostname, string port)
        {
            cli = new TcpClient(hostname, Convert.ToInt32(port));
            stream = cli.GetStream();
            reader = new StreamReader(stream);
            await StartListening();
        }

        private void Response(MessageType type, dynamic jsonobj)
        {
            return;
        }

        protected abstract void Response(APP_MANIFEST_OK type, dynamic message);
        protected abstract void Response(APP_DEPENDENCY type, dynamic message);
        protected abstract void Response(MSG_QUERY type, dynamic message);
        protected abstract void Response(MSG_BROADCAST type, dynamic message);


        private void Response(string badtype, dynamic jsonobj)
        {
            //This probably doesn't need to throw an error... a log might be sufficient.
            throw new ArgumentException("Invalid message type "+badtype+" recieved!");
        }

        public async Task StartListening()
        {
            await SendManifest();
            while (true)
            {
                dynamic obj = await ReadJson();
                string type = obj.type;
                dynamic message = obj.message;

                switch (type)
                {
                case "APP_MANIFEST": 
                    {
                        Response(new APP_MANIFEST(), message);
                        break;
                    }
                case "APP_MANIFEST_OK": 
                    {
                        Response(new APP_MANIFEST_OK(), message);
                        break;
                    }
                case "APP_MANIFEST_FAIL": 
                    {
                        Response(new APP_MANIFEST_FAIL(), message);
                        break;
                    }
                case "APP_DEPENDENCY":
                    {
                        Response(new APP_DEPENDENCY(), message);
                        break;
                    }
                case "MSG_QUERY":
                    {
                        Response(new MSG_QUERY(), message);
                        break;
                    }
                case "MSG_QUERY_SUCCESS":
                    {
                        Response(new MSG_QUERY_SUCCESS(), message);
                        break;
                    }
                case "MSG_QUERY_FAIL":
                    {
                        Response(new MSG_QUERY_FAIL(), message);
                        break;
                    }
                case "MSG_BROADCAST":
                    {
                        Response(new MSG_BROADCAST(), message);
                        break;
                    }
                case "MSG_BROADCAST_SUCCESS":
                    {
                        Response(new MSG_BROADCAST_SUCCESS(), message);
                        break;
                    }
                case "MSG_BROADCAST_FAIL":
                    {
                        Response(new MSG_BROADCAST_FAIL(), message);
                        break;
                    }
                default:
                    {
                        Response(type, message);
                        break;
                    }
                }
            }
        }


        public async void CloseConnection()
        {
            await SendData("APP_DOWN", "");
            cli.Close();
        }
        
        public async Task SendData(string type, string data)
        {
            string msg = type + " " + data;
            msg = msg.Trim();
            msg = Encoding.UTF8.GetByteCount(msg) + "\n" + msg;
            stream.Write(Encoding.UTF8.GetBytes(msg), 0, (int) msg.Length);
        }

        public async Task SendJson(string type, Object o)
        {            
            string obj = ser.Serialize(o);
            string msg = type + " " + obj;
            msg = msg.Trim();
            msg = Encoding.UTF8.GetByteCount(msg) + "\n" + msg;
            stream.Write(Encoding.UTF8.GetBytes(msg), 0, (int) msg.Length);
        }

        public async Task SendManifest()
        {
            await SendData("APP_MANIFEST", manifest);
        }

        public async Task<Object> ReadJson()
        {
            //Size of message in bytes
            string len = reader.ReadLine();
            var size = Convert.ToInt32(len);

            //buffer to put message in
            var buf = new Char[size];

            //Get the message
            await reader.ReadAsync(buf, 0, size);
            var str = new string(buf).Trim();
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
                return new
                {
                    type = match.Value,
                    message = new { }
                };
            }
            var obj = ser.Deserialize<dynamic>(jsonstr);

            //Return the type string and the object
            return new
            {
                type = match.Value,
                message = obj
            };
        }

    }
}
