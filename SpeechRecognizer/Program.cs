using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeechRecognizer
{
    class Program
    {
        
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Expected arguments in the form speechrecognizer host port");
                return;
            }
            var server = new SpeechServer();
            server.Connect(args[0], args[1]);
        }
    }
}
