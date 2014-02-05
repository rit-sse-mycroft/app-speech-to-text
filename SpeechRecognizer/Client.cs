using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Diagnostics;

namespace Mycroft.App
{
    /// <summary>
    /// The client abstract class. Any application made will inherit from
    /// this class.
    /// </summary>
    public abstract class Client
    {
        private string manifest;
        private TcpClient cli;
        private Stream stream;
        private JavaScriptSerializer ser = new JavaScriptSerializer();
        private StreamReader reader;
        protected MessageEventHandler handler;
        public string InstanceId;

        /// <summary>
        /// Constructor for a client
        /// </summary>
        public Client(string manifestPath)
        {
            var textStreamReader = new StreamReader(manifestPath);
            manifest = textStreamReader.ReadToEnd();
            var jsobj = ser.Deserialize<dynamic>(manifest);
            InstanceId = jsobj["instanceId"];
            handler = new MessageEventHandler();

        }

        #region Mycroft Connection
        /// <summary>
        /// Connects to Mycroft
        /// </summary>
        /// <param name="hostname">The hostname to connect to</param>
        /// <param name="port">The port to connect to</param>
        public async void Connect(string hostname, string port)
        {
            cli = new TcpClient(hostname, Convert.ToInt32(port));
            Logger.GetInstance().Info("Connected to Mycroft");
            stream = cli.GetStream();
            reader = new StreamReader(stream);
            try
            {
                await StartListening();
            }
            finally
            {
                CloseConnection();
            }
        }

        /// <summary>
        /// Start recieving messages from Mycroft
        /// </summary>
        /// <returns>An Awaitable Task</returns>
        public async Task StartListening()
        {
            await SendManifest();
            handler.Handle("CONNECT");
            while (true)
            {
                dynamic obj = await ReadJson();
                string type = obj.type;
                dynamic message = obj.message;
                handler.Handle(type, message);
            }
        }


        /// <summary>
        /// Close the connection from the Mycroft server
        /// </summary>
        public async void CloseConnection()
        {
            handler.Handle("END");
            await SendData("APP_DOWN");
            Logger.GetInstance().Info("Disconnected from Mycroft");
            cli.Close();
        }
        #endregion
        #region Message Sending and Recieving
        /// <summary>
        /// Send a message to Mycroft where data is a string. Used for sending
        /// Messages with no body (and app manifest because it's already a string)
        /// </summary>
        /// <param name="type">The type of message being sent</param>
        /// <param name="data">The message string that you are sending</param>
        /// <returns>A task</returns>
        private async Task SendData(string type, string data = "")
        {
            string msg = type + " " + data;
            msg = msg.Trim();
            msg = Encoding.UTF8.GetByteCount(msg) + "\n" + msg;
            Logger.GetInstance().Info("Sending Message " + type);
            Logger.GetInstance().Debug(msg);
            stream.Write(Encoding.UTF8.GetBytes(msg), 0, (int)msg.Length);
        }

        /// <summary>
        /// Sends a message to Mycroft where data is an object. Used for sending messages
        /// that actually have a body
        /// </summary>
        /// <param name="type">The type of message being sent</param>
        /// <param name="data">The json object being sent</param>
        /// <returns>A task</returns>
        private async Task SendJson(string type, Object data)
        {
            string obj = ser.Serialize(data);
            string msg = type + " " + obj;
            msg = msg.Trim();
            msg = Encoding.UTF8.GetByteCount(msg) + "\n" + msg;
            Logger.GetInstance().Info("Sending Message " + type);
            Logger.GetInstance().Debug(msg);
            stream.Write(Encoding.UTF8.GetBytes(msg), 0, (int)msg.Length);
        }

        /// <summary>
        /// Reads in json from the Mycroft server.
        /// </summary>
        /// <returns>An object with the type and message recieved</returns>
        private async Task<Object> ReadJson()
        {
            //Size of message in bytes
            string len = reader.ReadLine();
            var size = Convert.ToInt32(len);

            //buffer to put message in
            var buf = new Char[size];

            //Get the message
            reader.Read(buf, 0, size);
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
            Logger.GetInstance().Info("Recieved Message " + match.Value);
            Logger.GetInstance().Debug(jsonstr);
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
        #endregion
        #region Message Helpers
        /// <summary>
        /// Sends APP_MANIFEST to Mycroft
        /// </summary>
        /// <returns>A task</returns>
        public async Task SendManifest()
        {
            SendData("APP_MANIFEST", manifest);
        }

        /// <summary>
        /// Sends APP_UP to Mycroft
        /// </summary>
        /// <returns>A task</returns>
        public async Task Up()
        {
            SendData("APP_UP");
        }

        /// <summary>
        /// Sends APP_DOWN to Mycroft
        /// </summary>
        /// <returns>A task</returns>
        public async Task Down()
        {
            SendData("APP_DOWN");
        }

        /// <summary>
        /// Sends APP_IN_USE to Mycroft
        /// </summary>
        /// <param name="priority">the priority of the app</param>
        /// <returns>A task</returns>
        public async Task InUse(int priority)
        {
            SendJson("APP_IN_USE", new { priority = priority });
        }

        /// <summary>
        /// Sends MSG_BROADCAST to Mycroft
        /// </summary>
        /// <param name="content">The content object of the message</param>
        /// <returns>A task</returns>
        public async Task Broadcast(dynamic content)
        {
            var broadcast = new
            {
                id = Guid.NewGuid(),
                content = content
            };
            SendJson("MSG_BROADCAST", broadcast);
        }

        /// <summary>
        /// Sends MSG_QUERY to Mycroft
        /// </summary>
        /// <param name="capability">The capability</param>
        /// <param name="action">The action</param>
        /// <param name="data">The data of the message</param>
        /// <param name="instanceId">An array of instance ids. Defaults to null</param>
        /// <param name="priority">the priority. Defaults to 30</param>
        /// <returns>A task</returns>
        public async Task Query(string capability, string action, dynamic data, string[] instanceId = null, int priority = 30)
        {
            if (instanceId == null)
                instanceId = new string[0];
            var query = new
            {
                id = Guid.NewGuid(),
                capability = capability,
                action = action,
                data = data,
                instanceId = instanceId,
                priority = priority
            };
            SendJson("MSG_QUERY", query);
        }

        /// <summary>
        /// Sends MSG_QUERY_SUCCESS to Mycroft
        /// </summary>
        /// <param name="id">The id of the message being responded to</param>
        /// <param name="ret">The content of the message</param>
        /// <returns>A task</returns>
        public async Task QuerySuccess(string id, dynamic ret)
        {
            var querySuccess = new
            {
                id = id,
                ret = ret
            };
            SendJson("MSG_QUERY_SUCCESS", querySuccess);
        }

        /// <summary>
        /// Sends MSG_QUERY_FAIL to Mycroft
        /// </summary>
        /// <param name="id">The id of the message being responded to</param>
        /// <param name="message">The error message</param>
        /// <returns>A task</returns>
        public async Task QueryFail(string id, string message)
        {
            var queryFail = new
            {
                id = id,
                message = message
            };
            SendJson("MSG_QUERRY_FAIL", queryFail);
        }
        #endregion
    }
}