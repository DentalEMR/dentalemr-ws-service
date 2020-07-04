using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text;
using System.IO;

namespace DemrService.Utils
{
    public class BinaryExecuter
    {
        public delegate void FinishedHandler(int exitCode, string stdio, string stderr, string transactionId, string invocationId);

        protected string _binaryPath = null;
        protected string _workingDirectory = null;
        protected string _args = null;
        protected string _transactionid = null;
        protected string _invocationId = null;
        protected FinishedHandler _finishedHandler = null;

        private StringBuilder _output = null;
        private StringBuilder _error = null;
        

        public BinaryExecuter(string binaryPath, string args, string transactionId, string invocationId, FinishedHandler finishedHandler)
        {
            _binaryPath = binaryPath;
            _workingDirectory = Path.GetDirectoryName(_binaryPath);
            _args = args;
            _transactionid = transactionId;
            _invocationId = invocationId;
            _finishedHandler = finishedHandler;
        }
        public Task Execute()
        {
            return Task.Run(() =>
            {
                var process = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _binaryPath,
                        Arguments = _args,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = _workingDirectory
                    }
                };

                _output = new StringBuilder();
                process.OutputDataReceived += OutputHandler;

                _error = new StringBuilder();
                process.ErrorDataReceived += ErrorHandler;

                process.Start();
                //string result = process.StandardOutput.ReadToEnd();
                process.BeginOutputReadLine();
                process.WaitForExit();

                if (_finishedHandler != null)
                {
                    _finishedHandler(process.ExitCode, Output, Error, (_transactionid != null ? _transactionid : ""), _invocationId);
                }
                //return process.ExitCode; //method returns Task<int>
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
