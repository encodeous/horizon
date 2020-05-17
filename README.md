# Horizon - High performance Custom TCP Proxy and Port Tunneling over WebSockets.

## What is Horizon?

Horizon is a gateway that allows a client to map a remote tcp resource to a local port through a proxy server.

### What can Horizon do?

Horizon is useful if either end of the connection is restricted to HTTP/S connections (like behind a firewall).

For example, if you want to access a FTP server through a Firewall, Horizon can Obfuscate the ssh data through WebSockets and on the server Horizon will mirror the data sent by the client and connect to the FTP server as if the client was directly connecting to it.

### Main Features
- Access a resource through a firewall
- Blazingly fast! (Tested to handle ~5gbps with iperf)
- Built in Authentication (With a configuration wizard)
- Simple to use Command Line Interface
- Supports most operating systems
- Portable, no dependencies

## Great! This sounds like just what I need, how
This program could work through CDNs like CloudFlare (Untested) 
