using MelonLoader.TinyJSON;
using System.Text;
using UnityEngine.Networking;


namespace NeonNetwork.Online
{
    // https://gist.github.com/prodigga/861d72075f9f8abde5fc7b9744a1f4eb
    abstract class SSEDownloadHandlerBase(byte[] buffer) : DownloadHandlerScript(buffer)
    {
        static private readonly StringBuilder _currentLine = new();

        protected abstract void OnNewLineReceived(string line);

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            for (var i = 0; i < dataLength; i++)
            {
                var b = data[i];
                if (b == '\n')
                {
                    OnNewLineReceived(_currentLine.ToString());
                    _currentLine.Clear();
                }
                else
                    _currentLine.Append((char)b);
            }

            return true;
        }

        protected override void CompleteContent()
        {
            if (_currentLine.Length > 0)
                OnNewLineReceived(_currentLine.ToString());
        }
    }

    internal class NWIOEventHandler : SSEDownloadHandlerBase
    {
        public NWIOEventHandler() : base(new byte[1024]) { }

        protected override void OnNewLineReceived(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            try
            {
                var split = line.Split([':'], 2);
                var varient = JSON.Load(split[1].Trim());
                Online.OnEvent(split[0], varient);
            }
            catch
            {
                // malformed data, ignore
            }
        }
    }
}