using System;
using Renci.SshNet;

namespace EvFutBot.Utilities
{
    public class SshTunnel : IDisposable
    {
        private readonly SshClient _client;
        private readonly ForwardedPortLocal _port;

        public SshTunnel(ConnectionInfo connectionInfo, uint remotePort)
        {
            try
            {
                _client = new SshClient(connectionInfo);
                _port = new ForwardedPortLocal("127.0.0.1", 0, "127.0.0.1", remotePort);

                _client.Connect();
                _client.AddForwardedPort(_port);
                _port.Start();

                LocalPort = _port.BoundPort;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex.Message, ex.ToString());
                Dispose();
            }
        }

        public uint LocalPort { get; }

        public void Dispose()
        {
            _port?.Stop();
            _port?.Dispose();
            _client?.Disconnect();
            _client?.Dispose();

            Database.Tunnel = null;
        }
    }
}