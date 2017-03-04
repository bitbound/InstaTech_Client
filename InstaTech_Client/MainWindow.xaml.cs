#define Test
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Security.Principal;
using System.Net.WebSockets;
using System.Threading;
using System.IO;
using Newtonsoft.Json;
using System.Drawing.Imaging;
using System.Windows.Media.Animation;
using System.Net;
using System.Net.Http;
using System.Diagnostics;
using System.Security.Permissions;
using System.Net.NetworkInformation;
using Win32_Classes;

namespace InstaTech_Client
{
    public partial class MainWindow : Window
    {
        public static MainWindow Current { get; set; }

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
        const string downloadURI = "https://" + hostName + "/Downloads/InstaTech_Client.exe";
        const string versionURI = "https://" + hostName + "/Services/Get_Win_Client_Version.cshtml";

        // ***  Variables  *** //
        ClientWebSocket socket { get; set; }
        HttpClient httpClient = new HttpClient();
        Bitmap screenshot { get; set; }
        Bitmap lastFrame { get; set; }
        Bitmap croppedFrame { get; set; }
        byte[] newData;
        System.Drawing.Rectangle boundingBox { get; set; }
        Graphics graphic { get; set; }
        bool capturing = false;
        int totalHeight = 0;
        int totalWidth = 0;
        // Offsets are the left and top edge of the screen, in case multiple monitor setups
        // create a situation where the edge of a monitor is in the negative.  This must
        // be converted to a 0-based max left/top to render images on the canvas properly.
        int offsetX = 0;
        int offsetY = 0;
        System.Drawing.Point cursorPos;
        bool sendFullScreenshot = true;
        bool handleUAC = true;
        System.Timers.Timer uacTimer = new System.Timers.Timer(3000);

        public MainWindow()
        {
            InitializeComponent();
            Current = this;
            App.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
            checkArgs(Environment.GetCommandLineArgs().ToList());
            startUACTimer();
        }


        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var ping = new Ping();
            try
            {
                var response = await ping.SendPingAsync("translucency.info", 1000);
                if (response.Status != IPStatus.Success)
                {
                    System.Windows.MessageBox.Show("You don't appear to have an internet connection.  Please check your connection and try again.", "No Internet Connection", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                    return;
                }
            }
            catch
            {
                System.Windows.MessageBox.Show("You don't appear to have an internet connection.  Please check your connection and try again.", "No Internet Connection", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            initWebSocket();

            // Initialize variables requiring screen dimensions.
            totalWidth = SystemInformation.VirtualScreen.Width;
            totalHeight = SystemInformation.VirtualScreen.Height;
            offsetX = SystemInformation.VirtualScreen.Left;
            offsetY = SystemInformation.VirtualScreen.Top;
            screenshot = new Bitmap(totalWidth, totalHeight);
            lastFrame = new Bitmap(totalWidth, totalHeight);
            graphic = Graphics.FromImage(screenshot);

            // Clean up temp files from previous file transfers.
            var di = new DirectoryInfo(System.IO.Path.GetTempPath() + @"\InstaTech");
            if (di.Exists)
            {
                di.Delete(true);
            }
            await checkForUpdates(true);
        }
        private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            WriteToLog(e.Exception);
            System.Windows.MessageBox.Show("There was an error from which InstaTech couldn't recover.  If the issue persists, please contact the developer.", "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
            // *** Example of additional error handling. *** //
            //var result = System.Windows.MessageBox.Show("There was an error from which InstaTech couldn't recover.  Can we submit this error to the developer?  No personal information will be sent.", "Application Error", MessageBoxButton.YesNo, MessageBoxImage.Error);
            //if (result == MessageBoxResult.Yes)
            //{
            //    var httpClient = new HttpClient();
            //    var content = new MultipartFormDataContent();
            //    content.Add(new StringContent("InstaTech Client"), "app");
            //    content.Add(new StringContent("InstaTech User"), "name");
            //    content.Add(new StringContent("InstaTech User"), "from");
            //    content.Add(new StringContent("DoNotReply@translucency.info"), "email");
            //    var errors = WebUtility.HtmlEncode(File.ReadAllText(System.IO.Path.GetTempPath() + "InstaTech_Client_Errors.txt"));
            //    content.Add(new StringContent(errors), "message");
            //    var httpResponse = httpClient.PostAsync("https://translucency.info/Services/SendEmail.cshtml", content);
            //    httpResponse.Wait();
            //    if (httpResponse.Result.IsSuccessStatusCode)
            //    {
            //        System.Windows.MessageBox.Show("Thank you for helping me improve this app!", "Upload Success", MessageBoxButton.OK, MessageBoxImage.Information);
            //    }
            //    else
            //    {
            //        System.Windows.MessageBox.Show("The file upload failed.  Please send me an email if it persists.", "Upload Failed", MessageBoxButton.OK, MessageBoxImage.Information);
            //    }
            //}
            //else
            //{
            //    System.Windows.MessageBox.Show("Okay.  No information will be sent.", "Upload Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
            //}
        }
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }
        private void textSessionID_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            System.Windows.Clipboard.SetText(textSessionID.Text);
            textSessionID.SelectAll();
            showToolTip(textSessionID, "Copied to clipboard!", Colors.Green);
            
        }
        private void buttonMenu_Click(object sender, RoutedEventArgs e)
        {
            buttonMenu.ContextMenu.IsOpen = !buttonMenu.ContextMenu.IsOpen;
        }

        private void textAgentStatus_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (capturing)
            {
                capturing = false;
                socket.Dispose();
                stackMain.Visibility = Visibility.Collapsed;
                stackReconnect.Visibility = Visibility.Visible;
            }
        }

        private void textFilesTransferred_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var di = new DirectoryInfo(System.IO.Path.GetTempPath() + @"\InstaTech");
            if (di.Exists)
            {
                Process.Start("explorer.exe", di.FullName);
            }
            else
            {
                showToolTip(textFilesTransferred, "No files available.", Colors.Black);
            }
        }
        private async void menuUpgrade_Click(object sender, RoutedEventArgs e)
        {
            var services = System.ServiceProcess.ServiceController.GetServices();
            var itService = services.ToList().Find(sc => sc.ServiceName == "InstaTech_Service");
            if (itService != null)
            {
                System.Windows.MessageBox.Show("The InstaTech Service is already installed.  Please connect via Unattended Mode from the remote control.", "Service Already Installed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!WindowsIdentity.GetCurrent().Owner.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid))
            {
                System.Windows.MessageBox.Show("The client must be running as an administrator (i.e. elevated) in order to upgrade to a service.", "Elevation Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "InstaTech_Service.exe"), Properties.Resources.InstaTech_Service);
            }
            catch
            {
                System.Windows.MessageBox.Show("Failed to unpack the service into the temp directory.  Try clearing the temp directory.", "Write Failure", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            var psi = new ProcessStartInfo(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "InstaTech_Service.exe"), "-install -once");
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            Process.Start(psi);
            await socketSend(new {
                Type = "ConnectUpgrade",
                ComputerName = Environment.MachineName
            });
        }

        private void menuUnattended_Click(object sender, RoutedEventArgs e)
        {
            
            if (!WindowsIdentity.GetCurrent().Owner.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid))
            {
                System.Windows.MessageBox.Show("The client must be running as an administrator (i.e. elevated) in order to access unattended features.", "Elevation Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            new UnattendedWindow().ShowDialog();
        }
        private void menuUAC_Click(object sender, RoutedEventArgs e)
        {
            handleUAC = menuUAC.IsChecked;
        }
        private void menuAbout_Click(object sender, RoutedEventArgs e)
        {
            var win = new AboutWindow();
            win.Owner = this;
            win.ShowDialog();
        }
        private void buttonNewSession_Click(object sender, RoutedEventArgs e)
        {
            stackReconnect.Visibility = Visibility.Collapsed;
            stackMain.Visibility = Visibility.Visible;
            initWebSocket();
        }

        private async void showToolTip(FrameworkElement placementTarget, string message, System.Windows.Media.Color fontColor)
        {
            var tt = new System.Windows.Controls.ToolTip();
            tt.PlacementTarget = placementTarget;
            tt.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
            tt.HorizontalOffset = Math.Round(placementTarget.ActualWidth * .25, 0) * -1;
            tt.VerticalOffset = Math.Round(placementTarget.ActualHeight * .5, 0);
            tt.Content = message;
            tt.Foreground = new SolidColorBrush(Colors.Green);
            tt.IsOpen = true;
            await Task.Delay(message.Length * 50);
            tt.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromSeconds(1)));
            await Task.Delay(1000);
            tt.IsOpen = false;
            tt = null;
        }

        private async void initWebSocket()
        {
            try
            {
                socket = new ClientWebSocket();
            }
            catch (Exception ex)
            {
                WriteToLog(ex);
                System.Windows.MessageBox.Show("Unable to create web socket.", "Web Sockets Not Supported", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            try
            {
                await socket.ConnectAsync(new Uri(socketPath), CancellationToken.None);
            }
            catch (Exception ex)
            {
                WriteToLog(ex);
                System.Windows.MessageBox.Show("Unable to connect to server.", "Connection Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            // Send notification to server that this connection is for a client app.
            var request = new
            {
                Type = "ConnectionType",
                ConnectionType = "ClientApp",
                ComputerName = Environment.MachineName,
            };
            await socketSend(request);
            handleSocket();
        }
        private void checkArgs(List<string> args)
        {
            if (args.Exists(arg=>arg.Trim().ToLower() == "-install"))
            {
                try
                {
                    File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "InstaTech_Service.exe"), Properties.Resources.InstaTech_Service);
                    var psi = new ProcessStartInfo(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "InstaTech_Service.exe"), "-install");
                    psi.WindowStyle = ProcessWindowStyle.Hidden;
                    var proc = Process.Start(psi);
                    Environment.Exit(0);
                }
                catch
                {
                    Environment.Exit(1);
                }
            }
            else if (args.Exists(arg => arg.Trim().ToLower() == "-uninstall"))
            {
                try
                {
                    File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "InstaTech_Service.exe"), Properties.Resources.InstaTech_Service);
                    var psi = new ProcessStartInfo(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "InstaTech_Service.exe"), "-uninstall");
                    psi.WindowStyle = ProcessWindowStyle.Hidden;
                    var proc = Process.Start(psi);
                    Environment.Exit(0);
                }
                catch
                {
                    Environment.Exit(1);
                }
            }
            else if (args.Exists(arg => arg.Trim().ToLower() == "-update"))
            {
                try
                {
                    File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "InstaTech_Service.exe"), Properties.Resources.InstaTech_Service);
                    var psi = new ProcessStartInfo(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "InstaTech_Service.exe"), "-update");
                    psi.WindowStyle = ProcessWindowStyle.Hidden;
                    var proc = Process.Start(psi);
                    Environment.Exit(0);
                }
                catch
                {
                    Environment.Exit(1);
                }
            }
            else if (args.Count > 1 && File.Exists(args[1]))
            {
                var startTime = DateTime.Now;
                var success = false;
                while (success == false)
                {
                    Thread.Sleep(200);
                    if (DateTime.Now - startTime > TimeSpan.FromSeconds(10))
                    {
                        break;
                    }
                    try
                    {
                        File.Copy(System.Reflection.Assembly.GetExecutingAssembly().Location, args[1], true);
                        success = true;
                    }
                    catch
                    {
                        continue;
                    }
                }
                if (success == false)
                {
                    System.Windows.MessageBox.Show("Update failed.  Please close all InstaTech windows, then try again.", "Update Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    System.Windows.MessageBox.Show("Update successful!  InstaTech will now restart.", "Update Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    Process.Start(args[1]);
                }
                App.Current.Shutdown();
                return;
            }
        }
        private async void handleSocket()
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
                            case "SessionID":
                                textSessionID.Text = jsonMessage.SessionID;
                                textSessionID.Foreground = new SolidColorBrush(Colors.Black);
                                textSessionID.FontWeight = FontWeights.Bold;
                                break;
                            case "CaptureScreen":
                                beginScreenCapture();
                                break;
                            case "RTCOffer":
                                var request = new
                                {
                                    Type = "RTCOffer",
                                    ConnectionType = "Denied",
                                };
                                await socketSend(request);
                                break;
                            case "ConnectUpgrade":
                                if (jsonMessage.Status == "timeout")
                                {
                                    System.Windows.MessageBox.Show("The upgrade request timed out.", "Upgrade Timeout", MessageBoxButton.OK, MessageBoxImage.Warning);
                                }
                                else if (jsonMessage.Status == "nopartner")
                                {
                                    System.Windows.MessageBox.Show("You must have a partner connected before you can upgrade to a service.", "Partner Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                                }
                                else if (jsonMessage.Status == "ok")
                                {
                                    App.Current.Shutdown();
                                }
                                break;
                            case "RefreshScreen":
                                sendFullScreenshot = true;
                                break;
                            case "FileTransfer":
                                var url = jsonMessage.URL.ToString();
                                HttpResponseMessage httpResult = await httpClient.GetAsync(url);
                                var arrResult = await httpResult.Content.ReadAsByteArrayAsync();
                                string strFileName = jsonMessage.FileName.ToString();
                                var di = Directory.CreateDirectory(System.IO.Path.GetTempPath() + @"\InstaTech\");
                                File.WriteAllBytes(di.FullName + strFileName, arrResult);
                                textFilesTransferred.Text = di.GetFiles().Length.ToString();
                                showToolTip(textFilesTransferred, "File downloaded.", Colors.Black);
                                break;
                            case "SendClipboard":
                                byte[] arrData = Convert.FromBase64String(jsonMessage.Data.ToString());
                                System.Windows.Clipboard.SetText(Encoding.UTF8.GetString(arrData));
                                showToolTip(buttonMenu, "Clipboard data set.", Colors.Green);
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
                            case "PartnerClose":
                                capturing = false;
                                break;
                            case "PartnerError":
                                capturing = false;
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
            catch
            {
                capturing = false;
                stackMain.Visibility = Visibility.Collapsed;
                stackReconnect.Visibility = Visibility.Visible;
                textAgentStatus.FontWeight = FontWeights.Normal;
                textAgentStatus.Foreground = new SolidColorBrush(Colors.Black);
                textAgentStatus.Text = "Not Connected";
            }
        }
        // Remove trailing empty bytes in the buffer.
        private byte[] trimBytes(byte[] bytes)
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
        private async void beginScreenCapture()
        {
            capturing = true;
            sendFullScreenshot = true;
            this.WindowState = WindowState.Normal;
            showToolTip(textAgentStatus, "An agent is now viewing your screen.", Colors.Green);
            textAgentStatus.FontWeight = FontWeights.Bold;
            textAgentStatus.Foreground = new SolidColorBrush(Colors.Green);
            textAgentStatus.Text = "Connected";
            while (capturing == true)
            {
                await sendFrame();
                await Task.Delay(25);
            }
            stackMain.Visibility = Visibility.Collapsed;
            stackReconnect.Visibility = Visibility.Visible;
            textAgentStatus.FontWeight = FontWeights.Normal;
            textAgentStatus.Foreground = new SolidColorBrush(Colors.Black);
            textAgentStatus.Text = "Not Connected";
        }
        private async Task sendFrame()
        {
            if (!capturing)
            {
                return;
            }
            IntPtr hWnd = IntPtr.Zero;
            IntPtr hDC = IntPtr.Zero;
            IntPtr graphDC = IntPtr.Zero;
            try
            {
                hWnd = User32.GetDesktopWindow();
                hDC = User32.GetWindowDC(hWnd);
                graphDC = graphic.GetHdc();
                var copyResult = GDI32.BitBlt(graphDC, 0, 0, totalWidth, totalHeight, hDC, 0, 0, GDI32.TernaryRasterOperations.SRCCOPY | GDI32.TernaryRasterOperations.CAPTUREBLT);
                if (!copyResult)
                {
                    graphic.ReleaseHdc(graphDC);
                    graphic.Clear(System.Drawing.Color.White);
                    var font = new Font(System.Drawing.FontFamily.GenericSansSerif, 30, System.Drawing.FontStyle.Bold);
                    graphic.DrawString("Waiting for screen capture...", font, System.Drawing.Brushes.Black, new PointF((totalWidth / 2), totalHeight / 2), new StringFormat() { Alignment = StringAlignment.Center });
                }
                else
                {
                    graphic.ReleaseHdc(graphDC);
                    User32.ReleaseDC(hWnd, hDC);
                }
            }
            catch
            {
                if (graphDC != IntPtr.Zero)
                {
                    graphic.ReleaseHdc(graphDC);
                }
                if (hDC != IntPtr.Zero)
                {
                    User32.ReleaseDC(hWnd, hDC);
                }
                return;
            }
            try
            {
                // Get cursor information to draw on the screenshot.
                var ci = new User32.CursorInfo();
                ci.cbSize = Marshal.SizeOf(ci);
                User32.GetCursorInfo(out ci);
                if (ci.flags == User32.CURSOR_SHOWING)
                {
                    using (var icon = System.Drawing.Icon.FromHandle(ci.hCursor))
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
                    await socketSend(request);
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

        private byte[] getChangedPixels(Bitmap bitmap1, Bitmap bitmap2)
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
        private async Task checkForUpdates(bool Silent)
        {
            WebClient webClient = new WebClient();
            HttpClient httpClient = new HttpClient();
            var strFilePath = System.IO.Path.GetTempPath() + System.IO.Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            HttpResponseMessage response;
            if (File.Exists(strFilePath))
            {
                File.Delete(strFilePath);
            }
            try
            {
                response = await httpClient.GetAsync(versionURI);
            }
            catch
            {
                if (!Silent)
                {
                    System.Windows.MessageBox.Show("Unable to contact the server.  Check your network connection or try again later.", "Server Unreachable", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
                return;
            }
            var strCurrentVersion = await response.Content.ReadAsStringAsync();
            var thisVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            var currentVersion = Version.Parse(strCurrentVersion);
            if (currentVersion != thisVersion && currentVersion > new Version(0,0,0,0))
            {
                var result = System.Windows.MessageBox.Show("A new version of InstaTech is available!  Would you like to download it now?  It's an instant and effortless process.", "New Version Available", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    await webClient.DownloadFileTaskAsync(new Uri(downloadURI), strFilePath);
                    Process.Start(strFilePath, "\"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\"");
                    App.Current.Shutdown();
                    return;
                }
            }
            else
            {
                if (!Silent)
                {
                    System.Windows.MessageBox.Show("InstaTech is up-to-date.", "No Updates", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
        private async Task socketSend(dynamic JsonRequest)
        {
            var jsonRequest = JsonConvert.SerializeObject(JsonRequest);
            var outBuffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(jsonRequest));
            await socket.SendAsync(outBuffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private void buttonClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void startUACTimer()
        {
            uacTimer.Elapsed += (object sender, System.Timers.ElapsedEventArgs args) =>
            {
                if (handleUAC)
                {
                    var consent = Process.GetProcessesByName("Consent");
                    if (consent.Length > 0)
                    {
                        foreach (var proc in consent)
                        {
                            proc.Kill();
                        }
                        System.Windows.MessageBox.Show("A UAC prompt was closed automatically.  This prevents your input from getting stuck, since this client can't interact with UAC prompts." + Environment.NewLine + Environment.NewLine + "To interact with UAC, install the service or do a one-time upgrade from the menu.", "UAC Handled", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            };
            uacTimer.Start();
        }
        public static void WriteToLog(Exception ex)
        {
            var exception = ex;
            var path = System.IO.Path.GetTempPath() + "InstaTech_Client_Logs.txt";
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
                File.AppendAllText(path, JsonConvert.SerializeObject(jsonError) + Environment.NewLine);
                exception = exception.InnerException;
            }
        }
        public static void WriteToLog(string Message)
        {
            var path = System.IO.Path.GetTempPath() + "InstaTech_Client_Logs.txt";
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
            File.AppendAllText(path, JsonConvert.SerializeObject(jsoninfo) + Environment.NewLine);
        }
    }
}