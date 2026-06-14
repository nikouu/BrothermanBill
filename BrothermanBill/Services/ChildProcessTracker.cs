using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace BrothermanBill.Services
{
    /// <summary>
    /// Assigns child processes to a Windows Job Object configured with
    /// KILL_ON_JOB_CLOSE. When this (parent) process exits — including a hard kill
    /// such as stopping the Visual Studio debugger, which does not run
    /// <see cref="IHostedService.StopAsync"/> — the job handle closes and Windows
    /// terminates every process in the job. This prevents orphaned Lavalink
    /// processes from lingering and holding port 2333.
    ///
    /// Based on the well-known ChildProcessTracker pattern. Best-effort: any failure
    /// leaves the job handle as <see cref="IntPtr.Zero"/> and <see cref="AddProcess"/>
    /// becomes a no-op, so it never breaks startup.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static class ChildProcessTracker
    {
        private static readonly IntPtr s_jobHandle;

        static ChildProcessTracker()
        {
            IntPtr handle = IntPtr.Zero;
            try
            {
                handle = CreateJobObject(IntPtr.Zero, $"BrothermanBill_ChildProcesses_{Environment.ProcessId}");
                if (handle == IntPtr.Zero)
                {
                    s_jobHandle = IntPtr.Zero;
                    return;
                }

                var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
                {
                    BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                    {
                        LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                    }
                };

                int length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
                IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
                try
                {
                    Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);
                    if (!SetInformationJobObject(handle, JobObjectInfoType.ExtendedLimitInformation, extendedInfoPtr, (uint)length))
                    {
                        handle = IntPtr.Zero;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(extendedInfoPtr);
                }
            }
            catch
            {
                handle = IntPtr.Zero;
            }

            s_jobHandle = handle;
        }

        /// <summary>
        /// Adds a process to the kill-on-exit job. Returns <c>false</c> if the job
        /// could not be created or the process could not be assigned.
        /// </summary>
        public static bool AddProcess(Process process)
        {
            if (s_jobHandle == IntPtr.Zero)
            {
                return false;
            }

            return AssignProcessToJobObject(s_jobHandle, process.Handle);
        }

        private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

        private enum JobObjectInfoType
        {
            ExtendedLimitInformation = 9,
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(IntPtr hJob, JobObjectInfoType infoType, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }
    }
}
