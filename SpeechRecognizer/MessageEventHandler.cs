using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mycroft.App
{
    /// <summary>
    /// Delegate for any received Messages
    /// </summary>
    /// <param name="data">The message body received from Mycroft</param>
    public delegate void MsgHandler(dynamic data);

    /// <summary>
    /// Delegate for Connect and Disconnect events
    /// </summary>
    public delegate void ConnectDisconnect();

    /// <summary>
    /// The Message Event Handler class. Manages all the events for a mycroft client.
    /// </summary>
    public class MessageEventHandler
    {
        private Dictionary<string, Delegate> events;

        /// <summary>
        /// Constructor for MessageEventhandler
        /// </summary>
        public MessageEventHandler()
        {
            events = new Dictionary<string, Delegate>();
        }

        /// <summary>
        /// Adds a Message Handler
        /// </summary>
        /// <param name="msgType">The type of message this should be added to</param>
        /// <param name="del">The delegate method handling the event</param>
        public void On(string msgType, MsgHandler del)
        {
            if (!events.ContainsKey(msgType))
                events.Add(msgType, null);
            events[msgType] = (MsgHandler)events[msgType] + del;
        }

        /// <summary>
        /// Adds a ConnectDisconnect Handler
        /// </summary>
        /// <param name="msgType">The type of message that this should be added to</param>
        /// <param name="del">The delegate method handling the event</param>
        public void On(string msgType, ConnectDisconnect del)
        {
            if (!events.ContainsKey(msgType))
                events.Add(msgType, null);
            events[msgType] = (ConnectDisconnect)events[msgType] + del;
        }

        /// <summary>
        /// Handles an event of a given type
        /// </summary>
        /// <param name="msgType">The message type</param>
        /// <param name="data">The date to be passed to the handler</param>
        public void Handle(string msgType, dynamic data = null)
        {
            if (events.ContainsKey(msgType))
            {
                if (data == null)
                {
                    ConnectDisconnect c = (ConnectDisconnect)events[msgType];
                    c();
                }
                else
                {
                    MsgHandler h = (MsgHandler)events[msgType];
                    h(data);
                }
            }
            else
            {
                Logger.GetInstance().Warning("Not handling Message: " + msgType);
            }
        }
    }
}
