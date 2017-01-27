# InstaTech Client

A screen sharing client (WPF and cross-platform) intended use in remote tech support.

### WPF Client (/InstaTech Client/)
**Compatibility:** Windows 8.1 and 10.  Use the cross-platform version for Windows 7.

The WPF version is a small, portable EXE for Windows 8.1 and 10.  It doesn't run on Windows 7 due to the lack of websocket support.

### Windows Service Client (/InstaTech_Service/)
**Compatibility:** Windows 8.1 and 10.

A Windows service that will listen for connections and launch the client in the logged-on user's session.

### Cross-Platform Client (/InstaTech CP/)
**Compatibility:** Windows 7, Linux, and Mac.

The cross-platform version is larger than the WPF and uses an installer.  However, it works on all versions of Windows, Linux, and Mac.  It's built with Electron (http://electron.atom.io).

### Remote Control
The WPF client and cross-platform client, when launched, will generate a random session code.  Enter that code into the web-based remote control to view the remote computer and/or take control.  I haven't yet open-sourced the server code, but I might in the future.

Computers that have the Windows service will show up in the Unattended mode.  This is currently inaccessible in the demo.  A private InstaTech server is required for it.

The remote control tool is currently located at https://instatech.org/Demo/Remote_Control.

### 3rd-Party Libraries
The InstaTech project uses the below 3rd-party libraries.  A huge thank you to the creators for these!  They are all awesome, and I highly encourage you to check them out.
- Fody: https://github.com/Fody/Fody
- Fody/Costura: https://github.com/Fody/Costura
- Robot.js: https://github.com/octalmage/robotjs/
- Adapter: https://github.com/webrtc/adapter
