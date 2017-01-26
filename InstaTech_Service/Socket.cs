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

namespace InstaTech_Service
{
    public static class Socket
    {
        // ***  Config: Change these variables for your environment.  *** //
        public static string downloadURI = "https://instatech.org/Demo/Downloads/InstaTech_Service.exe";
        public static string versionURI = "https://instatech.org/Demo/Services/Get_Service_Version.cshtml";
#if DEBUG
        static string socketPath = "ws://localhost:52422/Services/Remote_Control_Socket.cshtml";
#else
        static string socketPath = "wss://instatech.org/Demo/Services/Remote_Control_Socket.cshtml";
#endif
#if DEBUG
        static string fileTransferURI = "http://localhost:52422/Services/FileTransfer.cshtml";
#else
        static string fileTransferURI = "https://instatech.org/Demo/Services/FileTransfer.cshtml";
#endif

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

        public static async Task StartInteractive()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            //Initialize variables requiring screen dimensions.
            totalWidth = SystemInformation.VirtualScreen.Width;
            totalHeight = SystemInformation.VirtualScreen.Height;
            offsetX = SystemInformation.VirtualScreen.Left;
            offsetY = SystemInformation.VirtualScreen.Top;
            screenshot = new Bitmap(totalWidth, totalHeight);
            lastFrame = new Bitmap(totalWidth, totalHeight);
            graphic = Graphics.FromImage(screenshot);

            // Clean up temp files from previous file transfers.
            var di = new DirectoryInfo(Path.GetTempPath() + @"\InstaTech");
            if (di.Exists)
            {
                di.Delete(true);
            }

            await initWebSocket();

            // Send notification to server that this connection is for a client app.
            var request = new
            {
                Type = "ConnectionType",
                ConnectionType = "ClientConsole",
                ComputerName = Environment.MachineName
            };
            var strRequest = JsonConvert.SerializeObject(request);
            var buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(strRequest));
            await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
            await handleInteractiveSocket();
        }


        public static async void StartService()
        {
            await initWebSocket();
            // Send notification to server that this connection is for a client app.
            var request = new
            {
                Type = "ConnectionType",
                ConnectionType = "ClientService",
                ComputerName = Environment.MachineName
            };
            var strRequest = JsonConvert.SerializeObject(request);
            var buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(strRequest));
            await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
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
                writeToErrorLog(ex);
                return;
            }
            try
            {
                await socket.ConnectAsync(new Uri(socketPath), CancellationToken.None);
            }
            catch (Exception ex)
            {
                writeToErrorLog(ex);
                return;
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            writeToErrorLog(e.ExceptionObject as Exception);
        }
        static private async Task handleInteractiveSocket()
        {
            try
            {
                ArraySegment<byte> buffer;
                WebSocketReceiveResult result;
                string trimmedString = "";
                dynamic jsonMessage = new { };
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
                            case "CaptureScreen":
                                beginScreenCapture();
                                break;
                            case "RTCOffer":
                                var request = new
                                {
                                    Type = "RTCOffer",
                                    ConnectionType = "Denied",
                                };
                                var strRequest = JsonConvert.SerializeObject(request);
                                var outBuffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(strRequest));
                                await socket.SendAsync(outBuffer, WebSocketMessageType.Text, true, CancellationToken.None);
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
                                var di = Directory.CreateDirectory(System.IO.Path.GetTempPath() + @"\InstaTech\");
                                File.WriteAllBytes(di.FullName + strFileName, byteFileData);
                                break;
                            case "SendClipboard":
                                byte[] arrData = Convert.FromBase64String(jsonMessage.Data.ToString());
                                System.Windows.Clipboard.SetText(Encoding.UTF8.GetString(arrData));
                                break;
                            case "MouseMove":
                                User32.SetCursorPos((int)Math.Round(((double)jsonMessage.PointX * totalWidth + offsetX), 0), (int)Math.Round(((double)jsonMessage.PointY * totalHeight + offsetY), 0));
                                break;
                            case "MouseDown":
                                if (jsonMessage.Button == "Left")
                                {
                                    User32.sendLeftMouseDown((int)Math.Round(((double)jsonMessage.PointX * totalWidth + offsetX), 0), (int)Math.Round(((double)jsonMessage.PointY * totalHeight + offsetY), 0));
                                }
                                else if (jsonMessage.Button == "Right")
                                {
                                    User32.sendRightMouseDown((int)Math.Round(((double)jsonMessage.PointX * totalWidth + offsetX), 0), (int)Math.Round(((double)jsonMessage.PointY * totalHeight + offsetY), 0));
                                }
                                break;
                            case "MouseUp":
                                if (jsonMessage.Button == "Left")
                                {
                                    User32.sendLeftMouseUp((int)Math.Round(((double)jsonMessage.PointX * totalWidth + offsetX), 0), (int)Math.Round(((double)jsonMessage.PointY * totalHeight + offsetY), 0));
                                }
                                else if (jsonMessage.Button == "Right")
                                {
                                    User32.sendRightMouseUp((int)Math.Round(((double)jsonMessage.PointX * totalWidth + offsetX), 0), (int)Math.Round(((double)jsonMessage.PointY * totalHeight + offsetY), 0));
                                }
                                break;
                            case "TouchMove":
                                User32.GetCursorPos(out cursorPos);
                                User32.SetCursorPos((int)Math.Round(cursorPos.X + (double)jsonMessage.MoveByX * totalWidth), (int)Math.Round(cursorPos.Y + (double)jsonMessage.MoveByY * totalHeight));
                                break;
                            case "Tap":
                                User32.GetCursorPos(out cursorPos);
                                User32.sendLeftMouseDown(cursorPos.X, cursorPos.Y);
                                User32.sendLeftMouseUp(cursorPos.X, cursorPos.Y);
                                break;
                            case "TouchDown":
                                User32.GetCursorPos(out cursorPos);
                                User32.sendLeftMouseDown(cursorPos.X, cursorPos.Y);
                                break;
                            case "LongPress":
                                User32.GetCursorPos(out cursorPos);
                                User32.sendRightMouseDown(cursorPos.X, cursorPos.Y);
                                User32.sendRightMouseUp(cursorPos.X, cursorPos.Y);
                                break;
                            case "TouchUp":
                                User32.GetCursorPos(out cursorPos);
                                User32.sendLeftMouseUp(cursorPos.X, cursorPos.Y);
                                break;
                            case "KeyPress":
                                try
                                {
                                    string baseKey = jsonMessage.Key;
                                    string prefix = "";
                                    while (baseKey.FirstOrDefault() == '+' || baseKey.FirstOrDefault() == '^' || baseKey.FirstOrDefault() == '%')
                                    {
                                        prefix += baseKey.FirstOrDefault().ToString();
                                        baseKey = baseKey.Substring(1);
                                    }
                                    if (baseKey.Length > 1)
                                    {
                                        baseKey = baseKey.Replace("Arrow", "");
                                        baseKey = baseKey.Replace("PageDown", "PGDN");
                                        baseKey = baseKey.Replace("PageUp", "PGUP");
                                        baseKey = "{" + baseKey + "}";
                                    }
                                    SendKeys.SendWait(prefix + baseKey);
                                }
                                catch
                                {
                                    // TODO: Report missing keybind.
                                }
                                break;
                            case "PartnerClose":
                                Environment.Exit(0);
                                break;
                            case "PartnerError":
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
                writeToErrorLog(ex);
                Environment.Exit(2);
            }
        }

        private async static void handleServiceSocket()
        {
            try
            {
                ArraySegment<byte> buffer;
                WebSocketReceiveResult result;
                string trimmedString = "";
                dynamic jsonMessage = new { };
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
                                var procInfo = new ADVAPI32.PROCESS_INFORMATION();
                                var processResult = ADVAPI32.OpenProcessAsSystem(System.Reflection.Assembly.GetExecutingAssembly().Location + " -interactive", out procInfo);
                                if (processResult == false)
                                {
                                    var response = new
                                    {
                                        Type = "ProcessStartResult",
                                        Status = "failed"
                                    };
                                    var strRequest = JsonConvert.SerializeObject(response);
                                    var outBuffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(strRequest));
                                    await socket.SendAsync(outBuffer, WebSocketMessageType.Text, true, CancellationToken.None);
                                    writeToErrorLog(new Exception("Error opening interactive process.  Error Code: " + Marshal.GetLastWin32Error().ToString()));
                                }
                                else
                                {
                                    var response = new
                                    {
                                        Type = "ProcessStartResult",
                                        Status = "ok"
                                    };
                                    var strRequest = JsonConvert.SerializeObject(response);
                                    var outBuffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(strRequest));
                                    await socket.SendAsync(outBuffer, WebSocketMessageType.Text, true, CancellationToken.None);
                                }
                                break;
                            case "ServiceRunning":
                                writeToErrorLog(new Exception("Service is already running on another computer with the same name."));
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                writeToErrorLog(ex);
                StartService();
            }
        }

        // Remove trailing empty bytes in the buffer.
        static private byte[] trimBytes(byte[] bytes)
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
                var station = User32.OpenWindowStation("WinSta0", true, User32.ACCESS_MASK.MAXIMUM_ALLOWED);
                var result = User32.SetProcessWindowStation(station.DangerousGetHandle());
                var inputDesktop = User32.OpenInputDesktop();
                if (User32.SetThreadDesktop(inputDesktop) == false)
                {
                    var error = Marshal.GetLastWin32Error();
                    writeToErrorLog(new Exception("Failed to open input desktop.  Error: " + error.ToString()));
                }

                var hWnd = User32.GetDesktopWindow();
                var hDC = User32.GetWindowDC(hWnd);
                var graphDC = graphic.GetHdc();
                GDI32.BitBlt(graphDC, 0, 0, totalWidth, totalHeight, hDC, 0, 0, GDI32.TernaryRasterOperations.SRCCOPY | GDI32.TernaryRasterOperations.CAPTUREBLT);
                graphic.ReleaseHdc(graphDC);
                User32.ReleaseDC(hWnd, hDC);
                //IntPtr deskDC = GDI32.CreateDC("DISPLAY", null, null, IntPtr.Zero);
                //var graphDC = graphic.GetHdc();
                //GDI32.BitBlt(graphDC, 0, 0, totalWidth, totalHeight, deskDC, 0, 0, GDI32.TernaryRasterOperations.SRCCOPY | GDI32.TernaryRasterOperations.CAPTUREBLT);
                //graphic.ReleaseHdc(graphDC);
                //GDI32.DeleteDC(deskDC);
            }
            catch
            {
                graphic.Clear(System.Drawing.Color.White);
                var font = new Font(System.Drawing.FontFamily.GenericSansSerif, 30, System.Drawing.FontStyle.Bold);
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
                        graphic.DrawImage(icon.ToBitmap(), new Rectangle(cursorPos.X - offsetX, cursorPos.Y - offsetY, Cursor.Current.Size.Width, Cursor.Current.Size.Height));
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
                    var strRequest = JsonConvert.SerializeObject(request);
                    var outBuffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(strRequest));
                    await socket.SendAsync(outBuffer, WebSocketMessageType.Text, true, CancellationToken.None);
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
                newData = getChangedPixels(screenshot, lastFrame);
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
            catch
            {
                capturing = false;
            }
        }

        static private byte[] getChangedPixels(Bitmap bitmap1, Bitmap bitmap2)
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
        private static void writeToErrorLog(Exception ex)
        {
            var exception = ex;
            while (ex != null)
            {
                File.AppendAllText(System.IO.Path.GetTempPath() + "InstaTech_Service_Errors.txt", DateTime.Now.ToString() + "\t" + ex.Message + "\t" + ex.StackTrace + Environment.NewLine);
                ex = ex.InnerException;
            }
        }
    }
}
