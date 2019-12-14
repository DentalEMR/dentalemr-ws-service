using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text;

namespace DemrService.Utils
{
    public class BinaryExecuter
    {
        protected string _binaryName = null;
        protected string _args = null;

        private StringBuilder _output = null;
        private StringBuilder _error = null;

        public BinaryExecuter(string binaryName, string args)
        {
            _binaryName = binaryName;
            _args = args;
        }
        public async Task<int> Execute()
        {
            return await Task<string>.Run(() =>
            {
                var process = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _binaryName,
                        Arguments = _args,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    }
                };

                _output = new StringBuilder();
                process.OutputDataReceived += OutputHandler;

                _error = new StringBuilder();
                //process.ErrorDataReceived += ErrorHandler;

                process.Start();
                //string result = process.StandardOutput.ReadToEnd();
                process.BeginOutputReadLine();
                process.WaitForExit();

                return process.ExitCode;
            });

        }

        public string Output { get { return _output.ToString(); } }
        public string Error { get { return _error.ToString(); } }

        private void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            // Collect the sort command output.
            if (!String.IsNullOrEmpty(outLine.Data))
            {
                 _output.Append(outLine.Data);
            }
        }

        private void ErrorHandler(object sendingProcess, DataReceivedEventArgs errLine)
        {
            // Collect the sort command output.
            if (!String.IsNullOrEmpty(errLine.Data))
            {
                _error.Append(errLine.Data);
            }
        }

    }
 }
