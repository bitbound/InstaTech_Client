using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace Win32_Classes
{
    public static class ADVAPI32
    {
        #region Structs
        [StructLayout(LayoutKind.Sequential)]
        public struct SECURITY_ATTRIBUTES
        {
            public int Length;
            public IntPtr lpSecurityDescriptor;
            public bool bInheritHandle;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFO
        {
            public Int32 cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public Int32 dwX;
            public Int32 dwY;
            public Int32 dwXSize;
            public Int32 dwYSize;
            public Int32 dwXCountChars;
            public Int32 dwYCountChars;
            public Int32 dwFillAttribute;
            public Int32 dwFlags;
            public Int16 wShowWindow;
            public Int16 cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }
        #endregion

        #region Enums
        public enum LOGON_TYPE
        {
            LOGON32_LOGON_INTERACTIVE = 2,
            LOGON32_LOGON_NETWORK,
            LOGON32_LOGON_BATCH,
            LOGON32_LOGON_SERVICE,
            LOGON32_LOGON_UNLOCK = 7,
            LOGON32_LOGON_NETWORK_CLEARTEXT,
            LOGON32_LOGON_NEW_CREDENTIALS
        }
        public enum LOGON_PROVIDER
        {
            LOGON32_PROVIDER_DEFAULT,
            LOGON32_PROVIDER_WINNT35,
            LOGON32_PROVIDER_WINNT40,
            LOGON32_PROVIDER_WINNT50
        }
        [Flags]
        public enum CreateProcessFlags
        {
            CREATE_BREAKAWAY_FROM_JOB = 0x01000000,
            CREATE_DEFAULT_ERROR_MODE = 0x04000000,
            CREATE_NEW_CONSOLE = 0x00000010,
            CREATE_NEW_PROCESS_GROUP = 0x00000200,
            CREATE_NO_WINDOW = 0x08000000,
            CREATE_PROTECTED_PROCESS = 0x00040000,
            CREATE_PRESERVE_CODE_AUTHZ_LEVEL = 0x02000000,
            CREATE_SEPARATE_WOW_VDM = 0x00000800,
            CREATE_SHARED_WOW_VDM = 0x00001000,
            CREATE_SUSPENDED = 0x00000004,
            CREATE_UNICODE_ENVIRONMENT = 0x00000400,
            DEBUG_ONLY_THIS_PROCESS = 0x00000002,
            DEBUG_PROCESS = 0x00000001,
            DETACHED_PROCESS = 0x00000008,
            EXTENDED_STARTUPINFO_PRESENT = 0x00080000,
            INHERIT_PARENT_AFFINITY = 0x00010000
        }
        public enum TOKEN_TYPE : int
        {
            TokenPrimary = 1,
            TokenImpersonation = 2
        }

        public enum SECURITY_IMPERSONATION_LEVEL : int
        {
            SecurityAnonymous = 0,
            SecurityIdentification = 1,
            SecurityImpersonation = 2,
            SecurityDelegation = 3,
        }

        #endregion

        #region Constants
        public const int TOKEN_DUPLICATE = 0x0002;
        public const uint MAXIMUM_ALLOWED = 0x2000000;
        public const int CREATE_NEW_CONSOLE = 0x00000010;

        public const int IDLE_PRIORITY_CLASS = 0x40;
        public const int NORMAL_PRIORITY_CLASS = 0x20;
        public const int HIGH_PRIORITY_CLASS = 0x80;
        public const int REALTIME_PRIORITY_CLASS = 0x100;
        #endregion

        #region DLL Imports

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool CreateProcessAsUser(
            IntPtr hToken,
            string lpApplicationName,
            string lpCommandLine,
            ref SECURITY_ATTRIBUTES lpProcessAttributes,
            ref SECURITY_ATTRIBUTES lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool AllocateLocallyUniqueId(out IntPtr pLuid);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = false)]
        public static extern SECUR32.WinErrors LsaNtStatusToWinError(SECUR32.WinStatusCodes status);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool GetTokenInformation(
            IntPtr TokenHandle,
            SECUR32.TOKEN_INFORMATION_CLASS TokenInformationClass,
            IntPtr TokenInformation,
            uint TokenInformationLength,
            out uint ReturnLength);

        [DllImport("advapi32.dll", SetLastError = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool LogonUser(
            [MarshalAs(UnmanagedType.LPStr)] string pszUserName,
            [MarshalAs(UnmanagedType.LPStr)] string pszDomain,
            [MarshalAs(UnmanagedType.LPStr)] string pszPassword,
            int dwLogonType,
            int dwLogonProvider,
            out IntPtr phToken);

        [DllImport("advapi32", SetLastError = true), SuppressUnmanagedCodeSecurityAttribute]
        public static extern bool OpenProcessToken(IntPtr ProcessHandle, int DesiredAccess, ref IntPtr TokenHandle);
        [DllImport("advapi32.dll", EntryPoint = "DuplicateTokenEx")]
        public extern static bool DuplicateTokenEx(IntPtr ExistingTokenHandle, uint dwDesiredAccess,
            ref SECURITY_ATTRIBUTES lpThreadAttributes, int TokenType,
            int ImpersonationLevel, ref IntPtr DuplicateTokenHandle);


        #endregion

        public static bool OpenProcessAsSystem(string applicationName, out PROCESS_INFORMATION procInfo)
        {
            try
            {

                uint winlogonPid = 0;
                IntPtr hUserTokenDup = IntPtr.Zero, hPToken = IntPtr.Zero, hProcess = IntPtr.Zero;
                procInfo = new PROCESS_INFORMATION();

                // obtain the currently active session id; every logged on user in the system has a unique session id
                uint dwSessionId = Kernel32.WTSGetActiveConsoleSessionId();

                // Check for RDP session.  If active, use that session ID instead.
                var rdpSessionID = GetRDPSession();
                if (rdpSessionID > 0)
                {
                    dwSessionId = rdpSessionID;
                }
                
                // obtain the process id of the winlogon process that is running within the currently active session
                Process[] processes = Process.GetProcessesByName("winlogon");
                foreach (Process p in processes)
                {
                    if ((uint)p.SessionId == dwSessionId)
                    {
                        winlogonPid = (uint)p.Id;
                    }
                }

                // obtain a handle to the winlogon process
                hProcess = Kernel32.OpenProcess(MAXIMUM_ALLOWED, false, winlogonPid);

                // obtain a handle to the access token of the winlogon process
                if (!OpenProcessToken(hProcess, TOKEN_DUPLICATE, ref hPToken))
                {
                    Kernel32.CloseHandle(hProcess);
                    return false;
                }

                // Security attibute structure used in DuplicateTokenEx and CreateProcessAsUser
                // I would prefer to not have to use a security attribute variable and to just 
                // simply pass null and inherit (by default) the security attributes
                // of the existing token. However, in C# structures are value types and therefore
                // cannot be assigned the null value.
                SECURITY_ATTRIBUTES sa = new SECURITY_ATTRIBUTES();
                sa.Length = Marshal.SizeOf(sa);

                // copy the access token of the winlogon process; the newly created token will be a primary token
                if (!DuplicateTokenEx(hPToken, MAXIMUM_ALLOWED, ref sa, (int)SECURITY_IMPERSONATION_LEVEL.SecurityIdentification, (int)TOKEN_TYPE.TokenPrimary, ref hUserTokenDup))
                {
                    Kernel32.CloseHandle(hProcess);
                    Kernel32.CloseHandle(hPToken);
                    return false;
                }

                // By default CreateProcessAsUser creates a process on a non-interactive window station, meaning
                // the window station has a desktop that is invisible and the process is incapable of receiving
                // user input. To remedy this we set the lpDesktop parameter to indicate we want to enable user 
                // interaction with the new process.
                STARTUPINFO si = new STARTUPINFO();
                si.cb = (int)Marshal.SizeOf(si);
                si.lpDesktop = @"winsta0\default"; // interactive window station parameter; basically this indicates that the process created can display a GUI on the desktop

                // flags that specify the priority and creation method of the process
                uint dwCreationFlags = NORMAL_PRIORITY_CLASS | CREATE_NEW_CONSOLE;

                // create a new process in the current user's logon session
                bool result = CreateProcessAsUser(hUserTokenDup,        // client's access token
                                                null,                   // file to execute
                                                applicationName,        // command line
                                                ref sa,                 // pointer to process SECURITY_ATTRIBUTES
                                                ref sa,                 // pointer to thread SECURITY_ATTRIBUTES
                                                false,                  // handles are not inheritable
                                                dwCreationFlags,        // creation flags
                                                IntPtr.Zero,            // pointer to new environment block 
                                                null,                   // name of current directory 
                                                ref si,                 // pointer to STARTUPINFO structure
                                                out procInfo            // receives information about new process
                                                );

                // invalidate the handles
                Kernel32.CloseHandle(hProcess);
                Kernel32.CloseHandle(hPToken);
                Kernel32.CloseHandle(hUserTokenDup);

                return result;
            }
            catch
            {
                procInfo = new PROCESS_INFORMATION() { };
                return false;
            }
        }
        public static uint GetRDPSession()
        {
            IntPtr ppSessionInfo = IntPtr.Zero;
            Int32 count = 0;
            Int32 retval = WTSAPI32.WTSEnumerateSessions(WTSAPI32.WTS_CURRENT_SERVER_HANDLE, 0, 1, ref ppSessionInfo, ref count);
            Int32 dataSize = Marshal.SizeOf(typeof(WTSAPI32.WTS_SESSION_INFO));
            var sessList = new List<WTSAPI32.WTS_SESSION_INFO>();
            Int64 current = (int)ppSessionInfo;

            if (retval != 0)
            {
                for (int i = 0; i < count; i++)
                {
                    WTSAPI32.WTS_SESSION_INFO sessInf = (WTSAPI32.WTS_SESSION_INFO)Marshal.PtrToStructure((System.IntPtr)current, typeof(WTSAPI32.WTS_SESSION_INFO));
                    current += dataSize;
                    sessList.Add(sessInf);
                }
            }
            uint retVal = 0;
            var rdpSession = sessList.Find(ses => ses.pWinStationName.ToLower().Contains("rdp") && ses.State == 0);
            if (sessList.Exists(ses => ses.pWinStationName.ToLower().Contains("rdp") && ses.State == 0))
            {
                retVal = (uint)rdpSession.SessionID;
            }
            return retVal;
        }
    }
}
