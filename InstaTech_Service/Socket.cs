#define Test
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using Win32_Classes;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Net;

namespace InstaTech_Service
{
    public static class Socket
    {
        // ***  Config: Change these variables for your environment.  *** //
#if Deploy    
        const string hostName = "";
#elif Test
        const string hostName = "test.instatech.org";
#elif DEBUG
        const string hostName = "localhost:52422";
#elif !DEBUG
        const string hostName = "demo.instatech.org";
#endif
#if DEBUG && !Test
        const string socketPath = "ws://" + hostName + "/Services/Remote_Control_Socket.cshtml";
#else
        const string socketPath = "wss://" + hostName + "/Services/Remote_Control_Socket.cshtml";
#endif
        const string fileTransferURI = "https://" + hostName + "/Services/File_Transfer.cshtml";
        const string downloadURI = "https://" + hostName + "/Downloads/InstaTech Client.exe";
        const string versionURI = "https://" + hostName + "/Services/Get_Service_Version.cshtml";

        // ***  Variables  *** //
        static ClientWebSocket socket { get; set; }
        static HttpClient httpClient = new HttpClient();
        static Bitmap screenshot { get; set; }
        static Bitmap lastFrame { get; set; }
        static Bitmap croppedFrame { get; set; }
        static byte[] newData;
        static System.Drawing.Rectangle boundingBox { get; set; }
        static Graphics graphic { get; set; }
        static bool capturing = false;
        static int totalHeight = 0;
        static int totalWidth = 0;
        // Offsets are the left and top edge of the screen, in case multiple monitor setups
        // create a situation where the edge of a monitor is in the negative.  This must
        // be converted to a 0-based max left/top to render images on the canvas properly.
        static int offsetX = 0;
        static int offsetY = 0;
        static Point cursorPos;
        static bool sendFullScreenshot = true;
        static DateTime lastMessage = DateTime.Now;
        static System.Timers.Timer idleTimer = new System.Timers.Timer(5000);

        public static async Task StartInteractive()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            var notifierPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "Notifier.exe");
            Process.Start(notifierPath);
            //Initialize variables requiring screen dimensions.
            totalWidth = SystemInformation.VirtualScreen.Width;
            totalHeight = SystemInformation.VirtualScreen.Height;
            offsetX = SystemInformation.VirtualScreen.Left;
            offsetY = SystemInformation.VirtualScreen.Top;
            screenshot = new Bitmap(totalWidth, totalHeight);
            lastFrame = new Bitmap(totalWidth, totalHeight);
            graphic = Graphics.FromImage(screenshot);

            // Clean up temp files from previous file transfers.
            var path = System.IO.Path.GetTempPath() + @"\InstaTech\";
            if (System.Security.Principal.WindowsIdentity.GetCurrent().IsSystem)
            {
                path = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + @"\InstaTech\";
            }
            var di = new DirectoryInfo(path);
            if (di.Exists)
            {
                di.Delete(true);
            }
            // Start idle timer.
            idleTimer.Elapsed += (object sender, System.Timers.ElapsedEventArgs e) => {
                if (DateTime.Now - lastMessage > TimeSpan.FromMinutes(5))
                {
                    SocketSend(new
                    {
                        Type = "IdleTimeout"
                    }).Wait();

                    socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection timed out.", CancellationToken.None).Wait();
                }
            };
            idleTimer.Start();
            await initWebSocket();
            string connectionType;
            if (Environment.GetCommandLineArgs().ToList().Exists(str => str.ToLower() == "-once"))
            {
                connectionType = "ClientConsoleOnce";
            }
            else
            {
                connectionType = "ClientConsole";
            }
            // Send notification to server that this connection is for a client console app.
            var request = new
            {
                Type = "ConnectionType",
                ConnectionType = connectionType,
                ComputerName = Environment.MachineName
            };
            await SocketSend(request);
            await handleInteractiveSocket();
        }


        public static async void StartService()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            await initWebSocket();
            string connectionType;
            if (Environment.GetCommandLineArgs().ToList().Exists(str => str.ToLower() == "-once"))
            {
                connectionType = "ClientServiceOnce";
            }
            else
            {
                connectionType = "ClientService";
            }
            // Send notification to server that this connection is for a client service.
            var request = new
            {
                Type = "ConnectionType",
                ConnectionType = connectionType,
                ComputerName = Environment.MachineName
            };
            await SocketSend(request);
            handleServiceSocket();
        }

        private static async Task initWebSocket()
        {
            try
            {
                socket = new ClientWebSocket();
            }
            catch (Exception ex)
            {
                WriteToLog(ex);
                return;
            }
            try
            {
                await socket.ConnectAsync(new Uri(socketPath), CancellationToken.None);
            }
            catch (Exception ex)
            {
                WriteToLog(ex);
                return;
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            WriteToLog(e.ExceptionObject as Exception);
        }
        static private async Task handleInteractiveSocket()
        {
            try
            {
                ArraySegment<byte> buffer;
                WebSocketReceiveResult result;
                string trimmedString = "";
                dynamic jsonMessage = null;
                while (socket.State == WebSocketState.Connecting || socket.State == WebSocketState.Open)
                {
                    buffer = ClientWebSocket.CreateClientBuffer(65536, 65536);
                    result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                    lastMessage = DateTime.Now;
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        trimmedString = Encoding.UTF8.GetString(trimBytes(buffer.Array));
                        jsonMessage = JsonConvert.DeserializeObject<dynamic>(trimmedString);
                        
                        switch ((string)jsonMessage.Type)
                        {
                            case "CaptureScreen":
                                var thisProc = System.Diagnostics.Process.GetCurrentProcess();
                                var allProcs = System.Diagnostics.Process.GetProcessesByName("InstaTech_Service").Where(proc=>proc.SessionId == Process.GetCurrentProcess().SessionId);
                                foreach (var proc in allProcs)
                                {
                                    if (proc.Id != thisProc.Id)
                                    {
                                        proc.Kill();
                                    }
                                }
                                await checkForUpdates();
                                beginScreenCapture();
                                break;
                            case "RTCOffer":
                                var request = new
                                {
                                    Type = "RTCOffer",
                                    ConnectionType = "Denied",
                                };
                                await SocketSend(request);
                                break;
                            case "RefreshScreen":
                                sendFullScreenshot = true;
                                break;
                            case "FileTransfer":

                                var retrievalCode = jsonMessage.RetrievalCode.ToString();
                                var httpRequest = new
                                {
                                    Type = "Download",
                                    RetrievalCode = retrievalCode,
                                };
                                var httpResult = await httpClient.PostAsync(fileTransferURI, new StringContent(JsonConvert.SerializeObject(httpRequest)));
                                var strResult = await httpResult.Content.ReadAsStringAsync();
                                string strFileName = jsonMessage.FileName.ToString();
                                var byteFileData = Convert.FromBase64String(strResult);
                                var path = System.IO.Path.GetTempPath() + @"\InstaTech\";
                                if (System.Security.Principal.WindowsIdentity.GetCurrent().IsSystem)
                                {
                                    path = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + @"\InstaTech\";
                                }
                                var di = Directory.CreateDirectory(path);
                                File.WriteAllBytes(di.FullName + strFileName, byteFileData);
                                Process.Start("explorer.exe", di.FullName);
                                break;
                            case "SendClipboard":
                                byte[] arrData = Convert.FromBase64String(jsonMessage.Data.ToString());
                                System.Windows.Clipboard.SetText(Encoding.UTF8.GetString(arrData));
                                break;
                            case "MouseMove":
                                User32.SendMouseMove((double)jsonMessage.PointX, (double)jsonMessage.PointY, totalWidth, totalHeight, offsetX, offsetY);
                                break;
                            case "MouseDown":
                                if (jsonMessage.Button == "Left")
                                {
                                    User32.SendLeftMouseDown((int)Math.Round(((double)jsonMessage.PointX * totalWidth + offsetX), 0), (int)Math.Round(((double)jsonMessage.PointY * totalHeight + offsetY), 0), (bool)jsonMessage.Alt, (bool)jsonMessage.Ctrl, (bool)jsonMessage.Shift);
                                }
                                else if (jsonMessage.Button == "Right")
                                {
                                    User32.SendRightMouseDown((int)Math.Round(((double)jsonMessage.PointX * totalWidth + offsetX), 0), (int)Math.Round(((double)jsonMessage.PointY * totalHeight + offsetY), 0), (bool)jsonMessage.Alt, (bool)jsonMessage.Ctrl, (bool)jsonMessage.Shift);
                                }
                                break;
                            case "MouseUp":
                                if (jsonMessage.Button == "Left")
                                {
                                    User32.SendLeftMouseUp((int)Math.Round(((double)jsonMessage.PointX * totalWidth + offsetX), 0), (int)Math.Round(((double)jsonMessage.PointY * totalHeight + offsetY), 0), (bool)jsonMessage.Alt, (bool)jsonMessage.Ctrl, (bool)jsonMessage.Shift);
                                }
                                else if (jsonMessage.Button == "Right")
                                {
                                    User32.SendRightMouseUp((int)Math.Round(((double)jsonMessage.PointX * totalWidth + offsetX), 0), (int)Math.Round(((double)jsonMessage.PointY * totalHeight + offsetY), 0), (bool)jsonMessage.Alt, (bool)jsonMessage.Ctrl, (bool)jsonMessage.Shift);
                                }
                                break;
                            case "MouseWheel":
                                User32.SendMouseWheel((int)Math.Round((double)jsonMessage.DeltaY * -1));
                                break;
                            case "TouchMove":
                                User32.GetCursorPos(out cursorPos);
                                User32.SetCursorPos((int)Math.Round(cursorPos.X + (double)jsonMessage.MoveByX * totalWidth), (int)Math.Round(cursorPos.Y + (double)jsonMessage.MoveByY * totalHeight));
                                break;
                            case "Tap":
                                User32.GetCursorPos(out cursorPos);
                                User32.SendLeftMouseDown(cursorPos.X, cursorPos.Y, false, false, false);
                                User32.SendLeftMouseUp(cursorPos.X, cursorPos.Y, false, false, false);
                                break;
                            case "TouchDown":
                                User32.GetCursorPos(out cursorPos);
                                User32.SendLeftMouseDown(cursorPos.X, cursorPos.Y, false, false, false);
                                break;
                            case "LongPress":
                                User32.GetCursorPos(out cursorPos);
                                User32.SendRightMouseDown(cursorPos.X, cursorPos.Y, false, false, false);
                                User32.SendRightMouseUp(cursorPos.X, cursorPos.Y, false, false, false);
                                break;
                            case "TouchUp":
                                User32.GetCursorPos(out cursorPos);
                                User32.SendLeftMouseUp(cursorPos.X, cursorPos.Y, false, false, false);
                                break;
                            case "KeyPress":
                                try
                                {
                                    string baseKey = jsonMessage.Key;
                                    string modifier = "";
                                    if (jsonMessage.Modifers != null)
                                    {
                                        if ((jsonMessage.Modifiers as string[]).Contains("Alt"))
                                        {
                                            modifier += "%";
                                        }
                                        if ((jsonMessage.Modifiers as string[]).Contains("Control"))
                                        {
                                            modifier += "^";
                                        }
                                        if ((jsonMessage.Modifiers as string[]).Contains("Shift"))
                                        {
                                            modifier += "+";
                                        }
                                    }
                                    if (baseKey.Length > 1)
                                    {
                                        baseKey = baseKey.Replace("Arrow", "");
                                        baseKey = baseKey.Replace("PageDown", "PGDN");
                                        baseKey = baseKey.Replace("PageUp", "PGUP");
                                        if (!baseKey.StartsWith("{") && !baseKey.EndsWith("}"))
                                        {
                                            baseKey = "{" + baseKey + "}";
                                        }
                                    }
                                    SendKeys.SendWait(modifier + baseKey);
                                }
                                catch (Exception ex)
                                {
                                    WriteToLog(ex);
                                    WriteToLog("Missing keybind for " + jsonMessage.Key);
                                }
                                break;
                            case "CtrlAltDel":
                                User32.SendSAS(false);
                                break;
                            case "UninstallService":
                                WriteToLog("Service uninstall requested.");
                                Process.Start(System.Reflection.Assembly.GetExecutingAssembly().Location, "-uninstall").WaitForExit();
                                jsonMessage.Status = "ok";
                                await SocketSend(jsonMessage);
                                break;
                            case "PartnerClose":
                                if (Environment.GetCommandLineArgs().ToList().Exists(str => str.ToLower() == "-once"))
                                {
                                    foreach (var proc in Process.GetProcessesByName("InstaTech_Service"))
                                    {
                                        if (proc.Id != Process.GetCurrentProcess().Id)
                                        {
                                            proc.Kill();
                                        }
                                    }
                                    Process.Start("cmd", "/c sc delete InstaTech_Service");
                                }
                                Environment.Exit(0);
                                break;
                            case "PartnerError":
                                foreach (var proc in Process.GetProcessesByName("InstaTech_Service"))
                                {
                                    if (proc.Id != Process.GetCurrentProcess().Id)
                                    {
                                        proc.Kill();
                                    }
                                }
                                Process.Start("cmd", "/c sc delete InstaTech_Service");
                                Environment.Exit(0);
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                foreach (var proc in Process.GetProcessesByName("InstaTech_Service"))
                {
                    if (proc.Id != Process.GetCurrentProcess().Id)
                    {
                        proc.Kill();
                    }
                }
                Process.Start("cmd", "/c sc delete InstaTech_Service");
                WriteToLog(ex);
                Environment.Exit(1);
            }
        }

        private async static void handleServiceSocket()
        {
            try
            {
                ArraySegment<byte> buffer;
                WebSocketReceiveResult result;
                string trimmedString = "";
                dynamic jsonMessage = null;
                while (socket.State == WebSocketState.Connecting || socket.State == WebSocketState.Open)
                {
                    buffer = ClientWebSocket.CreateClientBuffer(65536, 65536);
                    result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        trimmedString = Encoding.UTF8.GetString(trimBytes(buffer.Array));
                        jsonMessage = JsonConvert.DeserializeObject<dynamic>(trimmedString);

                        switch ((string)jsonMessage.Type)
                        {
                            case "ConnectUnattended":
                                foreach (var proc in Process.GetProcessesByName("InstaTech_Service"))
                                {
                                    if (proc.SessionId != Process.GetCurrentProcess().SessionId)
                                    {
                                        proc.Kill();
                                    }
                                }
                                var deskName = User32.GetCurrentDesktop();
                                var procInfo = new ADVAPI32.PROCESS_INFORMATION();
                                var processResult = ADVAPI32.OpenInteractiveProcess(System.Reflection.Assembly.GetExecutingAssembly().Location + " -interactive", deskName.ToLower(), out procInfo);
                                if (processResult == false)
                                {
                                    var response = new
                                    {
                                        Type = "ProcessStartResult",
                                        Status = "failed"
                                    };
                                    await SocketSend(response);
                                    WriteToLog(new Exception("Error opening interactive process.  Error Code: " + Marshal.GetLastWin32Error().ToString()));
                                }
                                else
                                {
                                    var response = new
                                    {
                                        Type = "ProcessStartResult",
                                        Status = "ok"
                                    };
                                    await SocketSend(response);
                                }
                                break;
                            case "ConnectUnattendedOnce":
                                foreach (var proc in Process.GetProcessesByName("InstaTech_Service"))
                                {
                                    if (proc.SessionId != Process.GetCurrentProcess().SessionId)
                                    {
                                        proc.Kill();
                                    }
                                }
                                var pi = new ADVAPI32.PROCESS_INFORMATION();
                                ADVAPI32.OpenInteractiveProcess(System.Reflection.Assembly.GetExecutingAssembly().Location + " -interactive -once", User32.GetCurrentDesktop().ToLower(), out pi);
                                break;
                            case "ServiceDuplicate":
                                WriteToLog(new Exception("Service is already running on another computer with the same name."));
                                break;
                            case "CtrlAltDel":
                                User32.SendSAS(false);
                                break;
                            case "Uninstall":
                                System.Diagnostics.Process.Start(System.Reflection.Assembly.GetExecutingAssembly().Location, "-uninstall");
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WriteToLog(ex);
                throw ex;
            }
        }

        // Remove trailing empty bytes in the buffer.
        static public byte[] trimBytes(byte[] bytes)
        {
            // Loop backwards through array until the first non-zero byte is found.
            var firstZero = 0;
            for (int i = bytes.Length - 1; i >= 0; i--)
            {
                if (bytes[i] != 0)
                {
                    firstZero = i + 1;
                    break;
                }
            }
            if (firstZero == 0)
            {
                throw new Exception("Byte array is empty.");
            }
            // Return non-empty bytes.
            return bytes.Take(firstZero).ToArray();
        }
        static private async void beginScreenCapture()
        {
            capturing = true;
            sendFullScreenshot = true;
            while (capturing == true)
            {
                sendFrame();
                await Task.Delay(25);
            }
        }
        static async private void sendFrame()
        {
            if (!capturing)
            {
                return;
            }

            try
            {
                var hWnd = User32.GetDesktopWindow();
                var hDC = User32.GetWindowDC(hWnd);
                var graphDC = graphic.GetHdc();
                var copyResult = GDI32.BitBlt(graphDC, 0, 0, totalWidth, totalHeight, hDC, 0, 0, GDI32.TernaryRasterOperations.SRCCOPY | GDI32.TernaryRasterOperations.CAPTUREBLT);
                // Switch desktop if copy fails.
                if (!copyResult)
                {
                    graphic.ReleaseHdc(graphDC);
                    User32.ReleaseDC(hWnd, hDC);
                    WriteToLog("Desktop switch initiated.");
                    var deskName = User32.GetCurrentDesktop();
                    var procInfo = new ADVAPI32.PROCESS_INFORMATION();
                    if (ADVAPI32.OpenInteractiveProcess(System.Reflection.Assembly.GetExecutingAssembly().Location + " -interactive", deskName.ToLower(), out procInfo))
                    {
                        var request = new
                        {
                            Type = "DesktopSwitch",
                            Status = "pending",
                            ComputerName = Environment.MachineName
                        };
                        await SocketSend(request);
                        capturing = false;
                    }
                    else
                    {
                        graphic.Clear(System.Drawing.Color.White);
                        var font = new Font(FontFamily.GenericSansSerif, 30, System.Drawing.FontStyle.Bold);
                        graphic.DrawString("Waiting for screen capture...", font, Brushes.Black, new PointF((totalWidth / 2), totalHeight / 2), new StringFormat() { Alignment = StringAlignment.Center });
                        var error = Marshal.GetLastWin32Error();
                        WriteToLog(new Exception("Failed to switch desktops.  Error: " + error.ToString()));
                    }
                }
                graphic.ReleaseHdc(graphDC);
                User32.ReleaseDC(hWnd, hDC);
            }
            catch
            {
                graphic.Clear(Color.White);
                var font = new Font(FontFamily.GenericSansSerif, 30, FontStyle.Bold);
                graphic.DrawString("Waiting for screen capture...", font, Brushes.Black, new PointF((totalWidth / 2), totalHeight / 2), new StringFormat() { Alignment = StringAlignment.Center });
            }
            try
            {
                // Get cursor information to draw on the screenshot.
                User32.GetCursorPos(out cursorPos);
                var ci = new User32.CursorInfo();
                ci.cbSize = Marshal.SizeOf(ci);
                User32.GetCursorInfo(out ci);
                if (ci.flags == User32.CURSOR_SHOWING)
                {
                    using (var icon = Icon.FromHandle(ci.hCursor))
                    {
                        graphic.DrawIcon(icon, ci.ptScreenPos.x, ci.ptScreenPos.y);
                    }
                }
                if (sendFullScreenshot)
                {
                    var request = new
                    {
                        Type = "Bounds",
                        Width = totalWidth,
                        Height = totalHeight
                    };
                    await SocketSend(request);
                    using (var ms = new MemoryStream())
                    {
                        screenshot.Save(ms, ImageFormat.Jpeg);
                        ms.WriteByte(0);
                        ms.WriteByte(0);
                        ms.WriteByte(0);
                        ms.WriteByte(0);
                        await socket.SendAsync(new ArraySegment<byte>(ms.ToArray()), WebSocketMessageType.Binary, true, CancellationToken.None);
                        sendFullScreenshot = false;
                        return;
                    }
                }
                newData = GetChangedPixels(screenshot, lastFrame);
                if (newData != null)
                {
                    using (var ms = new MemoryStream())
                    {
                        croppedFrame = screenshot.Clone(boundingBox, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                        croppedFrame.Save(ms, ImageFormat.Jpeg);
                        // Add x,y coordinates of top-left of image so receiver knows where to draw it.
                        foreach (var metaByte in newData)
                        {
                            ms.WriteByte(metaByte);
                        }
                        await socket.SendAsync(new ArraySegment<byte>(ms.ToArray()), WebSocketMessageType.Binary, true, CancellationToken.None);
                    }
                }
                lastFrame = (Bitmap)screenshot.Clone();
            }
            catch (Exception ex)
            {
                WriteToLog(ex);
            }
        }

        static private byte[] GetChangedPixels(Bitmap bitmap1, Bitmap bitmap2)
        {
            if (bitmap1.Height != bitmap2.Height || bitmap1.Width != bitmap2.Width)
            {
                throw new Exception("Bitmaps are not of equal dimensions.");
            }
            if (!Bitmap.IsAlphaPixelFormat(bitmap1.PixelFormat) || !Bitmap.IsAlphaPixelFormat(bitmap2.PixelFormat) ||
                !Bitmap.IsCanonicalPixelFormat(bitmap1.PixelFormat) || !Bitmap.IsCanonicalPixelFormat(bitmap2.PixelFormat))
            {
                throw new Exception("Bitmaps must be 32 bits per pixel and contain alpha channel.");
            }
            var width = bitmap1.Width;
            var height = bitmap1.Height;
            byte[] newImgData;
            int left = int.MaxValue;
            int top = int.MaxValue;
            int right = int.MinValue;
            int bottom = int.MinValue;

            var bd1 = bitmap1.LockBits(new System.Drawing.Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, bitmap1.PixelFormat);
            var bd2 = bitmap2.LockBits(new System.Drawing.Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, bitmap2.PixelFormat);
            // Get the address of the first line.
            IntPtr ptr1 = bd1.Scan0;
            IntPtr ptr2 = bd2.Scan0;

            // Declare an array to hold the bytes of the bitmap.
            int bytes = Math.Abs(bd1.Stride) * screenshot.Height;
            byte[] rgbValues1 = new byte[bytes];
            byte[] rgbValues2 = new byte[bytes];

            // Copy the RGBA values into the array.
            Marshal.Copy(ptr1, rgbValues1, 0, bytes);
            Marshal.Copy(ptr2, rgbValues2, 0, bytes);

            // Check RGBA value for each pixel.
            for (int counter = 0; counter < rgbValues1.Length - 4; counter += 4)
            {
                if (rgbValues1[counter] != rgbValues2[counter] ||
                    rgbValues1[counter + 1] != rgbValues2[counter + 1] ||
                    rgbValues1[counter + 2] != rgbValues2[counter + 2] ||
                    rgbValues1[counter + 3] != rgbValues2[counter + 3])
                {
                    // Change was found.
                    var pixel = counter / 4;
                    var row = (int)Math.Floor((double)pixel / bd1.Width);
                    var column = pixel % bd1.Width;
                    if (row < top)
                    {
                        top = row;
                    }
                    if (row > bottom)
                    {
                        bottom = row;
                    }
                    if (column < left)
                    {
                        left = column;
                    }
                    if (column > right)
                    {
                        right = column;
                    }
                }
            }
            if (left < right && top < bottom)
            {
                // Bounding box is valid.

                left = Math.Max(left - 20, 0);
                top = Math.Max(top - 20, 0);
                right = Math.Min(right + 20, totalWidth);
                bottom = Math.Min(bottom + 20, totalHeight);

                // Byte array that indicates top left coordinates of the image.
                newImgData = new byte[4];
                newImgData[0] = Byte.Parse(left.ToString().PadLeft(4, '0').Substring(0, 2));
                newImgData[1] = Byte.Parse(left.ToString().PadLeft(4, '0').Substring(2, 2));
                newImgData[2] = Byte.Parse(top.ToString().PadLeft(4, '0').Substring(0, 2));
                newImgData[3] = Byte.Parse(top.ToString().PadLeft(4, '0').Substring(2, 2));

                boundingBox = new System.Drawing.Rectangle(left, top, right - left, bottom - top);
                bitmap1.UnlockBits(bd1);
                bitmap2.UnlockBits(bd2);
                return newImgData;
            }
            else
            {
                bitmap1.UnlockBits(bd1);
                bitmap2.UnlockBits(bd2);
                return null;
            }
        }
        static private async Task SocketSend(dynamic JsonRequest)
        {
            var jsonRequest = JsonConvert.SerializeObject(JsonRequest);
            var outBuffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(jsonRequest));
            await socket.SendAsync(outBuffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }
        public static void WriteToLog(Exception ex)
        {
            var exception = ex;
            var path = System.IO.Path.GetTempPath() + "InstaTech_Service_Logs.txt";
            while (exception != null)
            {
                var jsonError = new
                {
                    Type = "Error",
                    Timestamp = DateTime.Now.ToString(),
                    Message = exception?.Message,
                    Source = exception?.Source,
                    StackTrace = exception?.StackTrace,
                };
                if (System.Security.Principal.WindowsIdentity.GetCurrent().IsSystem)
                {
                    path = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + @"\InstaTech_Service_Logs.txt";
                }
                File.AppendAllText(path, JsonConvert.SerializeObject(jsonError) + Environment.NewLine);
                exception = exception.InnerException;
            }
        }
        static private async Task checkForUpdates()
        {
            WebClient webClient = new WebClient();
            HttpClient httpClient = new HttpClient();
            Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + @"\InstaTech\");
            var strFilePath = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + @"\InstaTech\" + Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            HttpResponseMessage response;
            if (File.Exists(strFilePath))
            {
                File.Delete(strFilePath);
            }
            try
            {
                response = await httpClient.GetAsync(Socket.versionURI);

            }
            catch
            {
                return;
            }
            var strCurrentVersion = await response.Content.ReadAsStringAsync();
            var thisVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            var currentVersion = Version.Parse(strCurrentVersion);
            if (currentVersion != thisVersion && currentVersion > new Version(0, 0, 0, 0))
            {
                var request = new
                {
                    Type = "ClientUpdating",
                };
                await SocketSend(request);
                Socket.WriteToLog("Update download initiated.");
                await webClient.DownloadFileTaskAsync(new Uri(Socket.downloadURI), strFilePath);
                Socket.WriteToLog("Download complete.  Launching file.");
                Process.Start(strFilePath, "-update");
                Environment.Exit(0);
                return;
            }
        }
        public static void WriteToLog(string Message)
        {
            var path = Path.GetTempPath() + "InstaTech_Service_Logs.txt";
            if (File.Exists(path))
            {
                var fi = new FileInfo(path);
                while (fi.Length > 1000000)
                {
                    var content = File.ReadAllLines(path);
                    File.WriteAllLines(path, content.Skip(10));
                    fi = new FileInfo(path);
                }
            }
            var jsoninfo = new
            {
                Type = "Info",
                Timestamp = DateTime.Now.ToString(),
                Message = Message
            };
            if (System.Security.Principal.WindowsIdentity.GetCurrent().IsSystem)
            {
                path = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + @"\InstaTech_Service_Logs.txt";
            }
            File.AppendAllText(path, JsonConvert.SerializeObject(jsoninfo) + Environment.NewLine);
        }
    }
}
