using System;
using Microsoft.Tools.ServiceModel.Svcutil;
using System.Linq;
using System.Text.RegularExpressions;

namespace UpdateServiceRefOptions_Folder_With_Spaces
{
    class Program
    {
        static int Main(string[] args)
        {
            var re = new Regex(@"'[^\""]*'|[^\""^\s]+|""[^\""]*""");
            string optstring = @"-u  -v minimal";
            string[] opts = re.Matches(optstring).Cast<Match>().Select(m => m.Value).ToArray();
            return Tool.Main(opts);
        }
    }
}