// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal unsafe static partial class Sys
    {
        internal enum SysConfName
        {
            _SC_CLK_TCK = 1,
            _SC_PAGESIZE = 2,
            _SC_NPROCESSORS_ONLN = 3,
        }

        [DllImport(Interop.Libraries.SystemNative, EntryPoint = "SystemNative_SysConf", SetLastError = true)]
        internal static extern long SysConf(SysConfName name);
    }
}
