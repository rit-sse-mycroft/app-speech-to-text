using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace Mycroft.Manifest
{
    [DataContract]
    public class Manifest
    {
        [DataMember(Name = "version", IsRequired = true )]
        public string Version { get; set;  }
        [DataMember(Name = "name", IsRequired = true)]
        public string Name { get; set;  }
        [DataMember(Name = "displayName", IsRequired = true)]
        public string DisplayName { get; set;  }
        [DataMember(Name = "instanceId")]
        public string InstanceId { get; set;  }
        [DataMember(Name = "API", IsRequired = true)]
        public int API { get; set;  }
        [DataMember(Name = "description", IsRequired = true)]
        public string Description;
        [DataMember(Name = "capabilities")]
        public Dictionary<string, string> Capabilities { get; set; }
        [DataMember(Name = "dependencies")]
        public Dictionary<string, string> Dependencies { get; set; }

        public static Manifest parse(string manifestJson)
        {
            var serializer = new DataContractJsonSerializer(typeof(Manifest));
            Manifest manifest;
            var memStream = new MemoryStream(Encoding.UTF8.GetBytes(manifestJson));
            manifest = serializer.ReadObject(memStream) as Manifest;
            return manifest;
        }
    }
}
