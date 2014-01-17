using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mycroft.App.Message
{
    public abstract class MessageType {}

    public class APP_MANIFEST : MessageType
    {
        public static string TypeString() {
            return "APP_MANIFEST";
        }
    }

    public class APP_MANIFEST_OK : MessageType
    {
        public static string TypeString()
        {
            return "APP_MANIFEST_OK";
        }
    }

    public class APP_MANIFEST_FAIL : MessageType
    {
        public static string TypeString()
        {
            return "APP_MANIFEST_FAIL";
        }
    }

    public class APP_DEPENDENCY : MessageType
    {
        public static string TypeString()
        {
            return "APP_DEPENDENCY";
        }
    }

    public class MSG_QUERY : MessageType
    {
        public static string TypeString()
        {
            return "MSG_QUERY";
        }
    }

    public class MSG_QUERY_SUCCESS : MessageType
    {
        public static string TypeString()
        {
            return "MSG_QUERY_SUCCESS";
        }
    }

    public class MSG_QUERY_FAIL : MessageType
    {
        public static string TypeString()
        {
            return "MSG_QUERY_FAIL";
        }
    }

    public class MSG_BROADCAST : MessageType
    {
        public static string TypeString()
        {
            return "MSG_BROADCAST";
        }
    }

    public class MSG_BROADCAST_SUCCESS : MessageType
    {
        public static string TypeString()
        {
            return "MSG_BROADCAST_SUCCESS";
        }
    }

    public class MSG_BROADCAST_FAIL : MessageType
    {
        public static string TypeString()
        {
            return "MSG_BROADCAST_FAIL";
        }
    }

}
