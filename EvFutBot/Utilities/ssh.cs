using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
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

                // Hack to allow dynamic local ports, ForwardedPortLocal should expose _listener.LocalEndpoint
                FieldInfo memberInfo = typeof (ForwardedPortLocal).GetField("_listener",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (memberInfo == null) return;
                TcpListener listener = (TcpListener) memberInfo.GetValue(_port);
                LocalPort = ((IPEndPoint) listener.LocalEndpoint).Port;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex.Message, ex.ToString());
                Dispose();
            }
        }

        public int LocalPort { get; }

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