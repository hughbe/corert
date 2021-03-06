// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Runtime;
using System.Runtime.InteropServices;

namespace System.Runtime.CompilerServices
{
    // This structure is used to pass context about a type's static class construction state from the runtime
    // to the classlibrary via the CheckStaticClassConstruction callback. The runtime knows about the first
    // two fields (cctorMethodAddress and initialized) and thus these must remain the first two fields in the
    // same order and at the same offset (hence the sequential layout attribute). It is permissable for the
    // classlibrary to add its own fields after these for its own use however. These must not contain GC
    // references and will be zero initialized.
    [CLSCompliant(false)]
    [StructLayout(LayoutKind.Sequential)]
    public struct StaticClassConstructionContext
    {
        // Pointer to the code for the static class constructor method. This is initialized by the
        // binder/runtime.
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2111:PointersShouldNotBeVisible")]  
        public IntPtr cctorMethodAddress;

        // Initialization state of the class. This is initialized to 0. Every time managed code checks the
        // cctor state the runtime will call the classlibrary's CheckStaticClassConstruction with this context
        // structure unless initialized == 1. This check is specific to allow the classlibrary to store more
        // than a binary state for each cctor if it so desires.
        public volatile int initialized;
    }
}
