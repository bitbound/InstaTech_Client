# InstaTech Client

## InstaTech has been discontinued.  Please see the new project, Remotely, at https://github.com/jay-rad/remotely.

A screen sharing client (WPF and cross-platform) that runs on its accompanying ASP.NET server.

Server Repo: https://github.com/Jay-Rad/InstaTech_Server  
Website: https://instatech.lucency.co

### WPF Client + Windows Service (/InstaTech_Client/)
**Compatibility:** Windows 7, 8, and 10.

The WPF version is a small, portable EXE for Windows.  It contains the Windows Service and can be used from the command line in the same way.

**Switches**
   * -install = Installs (or updates) the service and begins accepting connections.
   * -uninstall = Uninstalls the service.

### Windows Service Client (/InstaTech_Service/)
**Compatibility:** Windows 7, 8, and 10.

A self-installing Windows service that will listen for connections and launch the client in the logged-on user's session.  This is embedded in the WPF client.

**Switches**
   * -install = Installs (or updates) the service and begins accepting connections.
   * -uninstall = Uninstalls the service.

### Cross-Platform Client (/InstaTech_CP/)
**Compatibility:** Windows, Linux, and Mac.

The cross-platform version is larger than the WPF and uses an installer.  However, it works on all versions of Windows, Linux, and Mac.  It's built with Electron (http://electron.atom.io).

### InstaTech Server
**Compatibility:** Windows 8/10 Pro or Enterprise, Windows Server 2012+.

Repo: https://github.com/Jay-Rad/InstaTech_Server

### Remote Control
The WPF client and cross-platform client, when launched, will generate a random session code.  Enter that code into the web-based remote control to view the remote computer and/or take control.

Computers that have the Windows service will show up in the Unattended mode and the Computer Hub of the InstaTech Server.

### Building
To customize the clients, change the hostname properties prior to building.  Check the comments in /InstaTech_Client/MainWindow.xaml.cs, /InstaTech_Service/Socket.cs, and /InstaTech_CP/app/main.js.

### 3rd-Party Libraries
The InstaTech project uses the below 3rd-party libraries.  A huge thank you to the creators for these!  They are all awesome, and I highly encourage you to check them out.
- Fody: https://github.com/Fody/Fody
- Fody/Costura: https://github.com/Fody/Costura
- Robot.js: https://github.com/octalmage/robotjs/
- System.Net.WebSockets.Client.Managed: https://github.com/PingmanTools/System.Net.WebSockets.Client.Managed
