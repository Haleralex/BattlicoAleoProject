using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace AleoCSharpLibrary
{
    internal class ShellExecutor
    {
        private static readonly PowerShell _ps = PowerShell.Create();

        public static PSDataCollection<PSObject> Command(string script)
        {
            string errorMsg = string.Empty;

            _ps.AddScript(script);

            _ps.AddCommand("Out-String");

            PSDataCollection<PSObject> outputCollection = new();
            _ps.Streams.Error.DataAdded += (object sender, DataAddedEventArgs e) =>
            {
                errorMsg = ((PSDataCollection<ErrorRecord>)sender)[e.Index].ToString();
            };


            IAsyncResult result = _ps.BeginInvoke<PSObject, PSObject>(null, outputCollection);

            _ps.EndInvoke(result);

            _ps.Commands.Clear();
            return outputCollection;
        }
    }
}
