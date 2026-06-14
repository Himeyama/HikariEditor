using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace HikariEditor
{
    // Windows 擬似コンソール (ConPTY) でシェルをホストする。
    // 手動でプロンプトを stdin に注入する方式と違い、シェルが本物の端末上で
    // 動くため、ssh・vim・python のように stdin を自分で消費する対話プロセスでも
    // 正しく動作し、プロンプト描画・エコー・リサイズもシェル任せにできる。
    internal sealed class ConPtySession : IDisposable
    {
        IntPtr hPC = IntPtr.Zero;
        IntPtr attrList = IntPtr.Zero;
        PROCESS_INFORMATION pi;

        // 自分で読み書きする端
        SafeFileHandle? inputWriteSide;
        SafeFileHandle? outputReadSide;
        // ConPTY に渡す端（解放まで開いたままにする）
        SafeFileHandle? inputReadSide;
        SafeFileHandle? outputWriteSide;

        public FileStream Input { get; private set; } = null!;   // シェルへの入力
        public FileStream Output { get; private set; } = null!;  // シェルからの出力

        public event Action? Exited;

        public void Start(string commandLine, string? workingDir, short cols, short rows)
        {
            // 端末が極端に小さいと一部のシェルが乱れるため最小サイズを確保
            if (cols < 1) cols = 80;
            if (rows < 1) rows = 24;

            // 入力用・出力用の 2 本のパイプを作る
            if (!CreatePipe(out inputReadSide, out inputWriteSide, IntPtr.Zero, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreatePipe(input) failed");
            if (!CreatePipe(out outputReadSide, out outputWriteSide, IntPtr.Zero, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreatePipe(output) failed");

            COORD size = new() { X = cols, Y = rows };
            int hr = CreatePseudoConsole(size, inputReadSide, outputWriteSide, 0, out hPC);
            if (hr != 0)
                throw new InvalidOperationException($"CreatePseudoConsole failed: 0x{hr:X8}");

            // PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE で擬似コンソールを子プロセスへ紐付ける
            STARTUPINFOEX siEx = default;
            siEx.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();

            IntPtr listSize = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref listSize);
            attrList = Marshal.AllocHGlobal(listSize);
            siEx.lpAttributeList = attrList;
            if (!InitializeProcThreadAttributeList(attrList, 1, 0, ref listSize))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "InitializeProcThreadAttributeList failed");
            if (!UpdateProcThreadAttribute(attrList, 0, PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                    hPC, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "UpdateProcThreadAttribute failed");

            bool ok = CreateProcess(
                null, commandLine, IntPtr.Zero, IntPtr.Zero,
                bInheritHandles: false,
                dwCreationFlags: EXTENDED_STARTUPINFO_PRESENT,
                lpEnvironment: IntPtr.Zero,
                lpCurrentDirectory: workingDir,
                ref siEx, out pi);
            if (!ok)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcess failed");

            Input = new FileStream(inputWriteSide, FileAccess.Write);
            Output = new FileStream(outputReadSide, FileAccess.Read);

            // 子プロセスの終了を監視して通知する
            Thread watcher = new(() =>
            {
                WaitForSingleObject(pi.hProcess, INFINITE);
                Exited?.Invoke();
            })
            { IsBackground = true };
            watcher.Start();
        }

        public void Resize(short cols, short rows)
        {
            if (hPC == IntPtr.Zero) return;
            if (cols < 1) cols = 1;
            if (rows < 1) rows = 1;
            ResizePseudoConsole(hPC, new COORD { X = cols, Y = rows });
        }

        public void Dispose()
        {
            // 擬似コンソールを閉じるとぶら下がっている子プロセスへ終了が伝わる
            if (hPC != IntPtr.Zero) { ClosePseudoConsole(hPC); hPC = IntPtr.Zero; }

            try { Input?.Dispose(); } catch { /* 解放時のエラーは無視 */ }
            try { Output?.Dispose(); } catch { /* 同上 */ }
            try { inputReadSide?.Dispose(); } catch { }
            try { outputWriteSide?.Dispose(); } catch { }

            if (attrList != IntPtr.Zero)
            {
                DeleteProcThreadAttributeList(attrList);
                Marshal.FreeHGlobal(attrList);
                attrList = IntPtr.Zero;
            }
            if (pi.hThread != IntPtr.Zero) { CloseHandle(pi.hThread); pi.hThread = IntPtr.Zero; }
            if (pi.hProcess != IntPtr.Zero) { CloseHandle(pi.hProcess); pi.hProcess = IntPtr.Zero; }
        }

        #region P/Invoke

        const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        const uint INFINITE = 0xFFFFFFFF;
        static readonly IntPtr PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = (IntPtr)0x00020016;

        [StructLayout(LayoutKind.Sequential)]
        struct COORD { public short X; public short Y; }

        [StructLayout(LayoutKind.Sequential)]
        struct STARTUPINFO
        {
            public int cb;
            public IntPtr lpReserved;
            public IntPtr lpDesktop;
            public IntPtr lpTitle;
            public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct STARTUPINFOEX
        {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CreatePipe(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, IntPtr lpPipeAttributes, int nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int CreatePseudoConsole(COORD size, SafeFileHandle hInput, SafeFileHandle hOutput, uint dwFlags, out IntPtr phPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int ResizePseudoConsole(IntPtr hPC, COORD size);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern void ClosePseudoConsole(IntPtr hPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool UpdateProcThreadAttribute(IntPtr lpAttributeList, uint dwFlags, IntPtr Attribute, IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CreateProcess(
            string? lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string? lpCurrentDirectory,
            ref STARTUPINFOEX lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        #endregion
    }
}
