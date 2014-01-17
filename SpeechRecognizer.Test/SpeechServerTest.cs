using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Serialization.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using SpeechRecognizer;
using Mycroft.Manifest;

namespace SpeechRecognizer.Test
{
    [TestClass]
    public class SpeechServerTest
    {
        [TestMethod]
        public async Task TestManifest()
        {

            var stream = new MemoryStream();
            var cli = new SpeechServer();
            cli.SetStream(stream);
            await cli.SendManifest();

            stream.Seek(0, SeekOrigin.Begin);
            var reader = new StreamReader(stream);

            var size = Convert.ToInt32(reader.ReadLine());
            var outp = new char[size];
            reader.ReadBlock(outp, 0, size);

            var str = new String(outp);

            var re = new Regex(@"^([A-Z_]*)");
            Assert.IsTrue(re.Match(str).Value == "APP_MANIFEST");
            var jsonstr = str.TrimStart(re.Match(str).Value.ToCharArray());

            var jsonstream = new MemoryStream(Encoding.UTF8.GetBytes(jsonstr));

            var ser = new DataContractJsonSerializer(typeof(Manifest));
            Manifest jsonobj = (Manifest)ser.ReadObject(jsonstream);
            Assert.IsTrue(jsonobj.Version == "0.0.1");
            Assert.IsTrue(jsonobj.Name == "speech-recognizer");
            Assert.IsTrue(jsonobj.DisplayName == "Mycroft Networked Speech Recognizer");
            Assert.IsTrue(jsonobj.Description == "Lets applications register speech triggers for Mycroft to look for.");
            Assert.IsTrue(jsonobj.InstanceId == "primary");
        }
    }
}
