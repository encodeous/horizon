# Horizon - High performance TCP Proxy and Port Tunneling over WebSockets.

![Banner](https://raw.githubusercontent.com/encodeous/horizon/master/banner.png)

## About Horizon

[![Build status](https://ci.appveyor.com/api/projects/status/mpqu71a30820p88d/branch/master?svg=true)](https://ci.appveyor.com/project/Encodeous/horizon/branch/master) | **[Quick Download](https://github.com/encodeous/horizon/releases)**

Horizon is a _gateway_ that allows a client to map a remote tcp resource to a local port through a _proxy_ server.

### Functionality

Horizon is useful if either end of the connection is _restricted_ to HTTP/S connections (like behind a firewall).

For example, if you want to access a SSH server through a _Firewall_ , Horizon can _Obfuscate_ the SSH data through WebSockets and on the server, Horizon will _mirror_ the data sent by the client and connect to the SSH server as if the client was directly connecting to it.

### Main Features
- Access a resource through a **firewall**
- **Blazingly fast**! (Tested to handle ~5Gbit/s with iperf)
- Built in **Authentication** (With an intuitive configuration wizard)
- **Simple to use** Command Line Interface
- Supports both **Windows** and **Linux**
- Portable, **no dependencies**
- _Could_ tunnel through **CDNs** like **CloudFlare** _(Not Officially Supported)_

## Getting Started

Horizon has a simple cli, the commands are as follows:

```
  -a, --about       About horizon

  -s, --server      [Server] Start horizon as a server

  -q, --authfile    [Server] Specify a custom path to an auth file

  -p, --port        [Server] Specify a port to listen to

  -c, --client      [Client] Start horizon as a client and connect to the specified horizon server --client <uri>

  -m, --portmap     [Client] Maps local ports to remote addresses. <port>:<remote_server>:<port> Example:
                    22:ssh.example.com:22

  -u, --user        [Client] Authentication username

  -t, --token       [Client] Authentication token (secret)

  -b, --buffer      [Client/Server] I/O buffer size

  -g, --config      [Util] Generate an auth file through a wizard

  --help            Display this help screen.

  --version         Display version information.
```

Here is an example of hosting a Horizon server on port 1234:

```
horizon-cli -s -p 1234
```

Here is another example of connecting to the server above and mirroring port 1235 (Client) to localhost:1236 (on the server) with Horizon.

```
horizon-cli -c ws://10.10.10.3:1234 -m 1235:localhost:1236
```

Connections can be made through the client on port 1234, and Horizon will tunnel the data and automagically mirror it on the Server side on port 1236.
