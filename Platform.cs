using System;
using System.Linq;
using System.Reflection;

namespace MarkerCheck
{
    public static class Platform
    {
        /// <summary>
        /// Don't change the order of these enum's.
        /// </summary>
        public enum SupportedMonoVersion
        {
            None,
            v2_10_8_1,
            v3_2_8,
            v4_2_1,
        }

        private static bool? _isMono;

        static Platform()
        {
            ApplicationArchitecture = IntPtr.Size == 8 ? 64 : 32;
        }

        public static bool IsLinux
        {
            get { return Environment.OSVersion.Platform == PlatformID.Unix; }
        }

        public static bool IsWindows
        {
            get { return !IsLinux; }
        }        

        public static bool IsMono
        {
            get {
                    if (_isMono == null)
                        _isMono = Type.GetType ("Mono.Runtime") != null;

                    return (bool)_isMono;
            }
        }


        public static bool IsDotNet
        {
            get { return !IsMono; }
        }

        public static int ApplicationArchitecture { get; private set; }
    }
}

