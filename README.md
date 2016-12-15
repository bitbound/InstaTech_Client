# InstaTech Client

A screen sharing client (WPF and cross-platform) intended use in remote tech support.

### WPF Client (/InstaTech Client/)
**Compatibility:** Windows 8.1 and 10.  Use the cross-platform version for Windows 7.

The WPF version is a small, portable EXE for Windows 8.1 and 10.  It doesn't run on Windows 7 due to the lack of websocket support.

### Cross-Platform Client (/InstaTech CP/)
**Compatibility:** Windows 7, Linux, and Mac.

The cross-platform version is larger than the WPF and uses an installer.  However, it works on all versions of Windows, Linux, and Mac.  It's built with Electron (http://electron.atom.io).

### Remote Control
Both versions, when launched, will generate a random session code.  Enter that code into the web-based remote control to view the remote computer and/or take control.  I haven't yet open-sourced the server code, but I might in the near future.

The remote control tool is currently located at https://instatech.org/ScreenViewer.
