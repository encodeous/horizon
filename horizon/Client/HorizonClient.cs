using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using horizon.Handshake;
using horizon.Transport;
using Microsoft.Extensions.Logging;
using wstreamlib;

namespace horizon.Client
{
    /// <summary>
    /// Allows for proxying of TCP connections to and from the server
    /// </summary>
    public class HorizonClient
    {
        private HorizonClientConfig _config;
        private WStream _stream;
        private Conduit _conduit;
        /// <summary>
        /// Configure the client
        /// </summary>
        /// <param name="config"></param>
        public HorizonClient(HorizonClientConfig config)
        {
            _config = config;
        }
        /// <summary>
        /// Start and Connect the client to the server
        /// </summary>
        /// <returns>Returns true if the client was successfully connected</returns>
        public async Task<bool> Start()
        {
            $"Connecting to {_config.Server}".Log(LogLevel.Information);
            // Start a WStream client
            _stream = new WStream();
            // Connect the wstream client
            var wsconn = await _stream.Connect(_config.Server);
            // Check if the security handshake is successful
            if (await ClientHandshake.SecurityHandshake(new BinaryAdapter(wsconn), _config))
            {
                _conduit = new Conduit(wsconn);
                if (_config.ProxyConfig is HorizonReverseProxyConfig rproxcfg)
                {
                    $"Started Reverse Proxy!".Log(LogLevel.Information);
                    new HorizonOutput(rproxcfg.LocalEndPoint, _conduit);
                }
                else if (_config.ProxyConfig is HorizonProxyConfig proxcfg)
                {
                    $"Started Proxy!".Log(LogLevel.Information);
                    new HorizonInput(new IPEndPoint(IPAddress.Any, proxcfg.LocalPort), _conduit);
                }
                return true;
            }
            $"Handshake Failed".Log(LogLevel.Debug);
            try
            {
                await wsconn.Close();
            }
            catch(Exception e)
            {
                $"{e.Message} {e.StackTrace}".Log(LogLevel.Trace);
            }
            return false;
        }

        public async Task Stop()
        {
            $"Shutting Down Client...".Log(LogLevel.Information);
            await _conduit.Disconnect();
            await _stream.Close();
        }
    }
}
