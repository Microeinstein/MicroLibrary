#pragma warning disable CS0162

using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
//using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Micro.IO;

namespace Micro {
    public static partial class Core {
        static FileStream debugFile;
        static StreamWriter debugWriter;

        public static void DebugStart(string folder) {
            if (debugFile != null)
                throw new Exception();
            var path = Path.Combine(folder, $"debug {DateTime.Now:u}.txt".Replace(':', '-'));
            debugFile = new FileStream(path, FileMode.Create);
            debugWriter = new StreamWriter(debugFile);
        }
        public static void DebugWriteLine(string text) {
            if (debugWriter != null)
                debugWriter.WriteLine(text);
            else if (false)
                Debug.WriteLine(text);
        }
        public static void DebugStop() {
            if (debugFile == null)
                return;
            debugWriter.Close();
            debugWriter.Dispose();
            debugWriter = null;
            debugFile.Dispose();
            debugFile = null;
        }

        public static ParamType FindType(object obj, bool fixedWhenPowerTwoArray = false) {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));
            switch (obj) {
                case bool _:
                    return ParamType.BOOLEAN;
                case char _:
                    return ParamType.CHAR;
                case sbyte _:
                    return ParamType.SBYTE;
                case byte _:
                    return ParamType.BYTE;
                case short _:
                    return ParamType.SHORT;
                case ushort _:
                    return ParamType.USHORT;
                case int _:
                    return ParamType.INT;
                case uint _:
                    return ParamType.UINT;
                case long _:
                    return ParamType.LONG;
                case ulong _:
                    return ParamType.ULONG;
                case float _:
                    return ParamType.FLOAT;
                case double _:
                    return ParamType.DOUBLE;
                case decimal _:
                    return ParamType.DECIMAL;
                case DateTime _:
                    return ParamType.DATETIME;
                case Guid _:
                    return ParamType.GUID;
                case string _:
                    return ParamType.STRING;
                //case BigInteger _:
                //    return ParamType.BIGINTEGER;
                case byte[] byteArray:
                    if (fixedWhenPowerTwoArray) {
                        if (byteArray.Length == 1)
                            return ParamType.BYTES_1;
                        else if (byteArray.Length == 2)
                            return ParamType.BYTES_2;
                        else if (byteArray.Length == 4)
                            return ParamType.BYTES_4;
                        else if (byteArray.Length == 8)
                            return ParamType.BYTES_8;
                        else if (byteArray.Length == 16)
                            return ParamType.BYTES_16;
                    }
                    return ParamType.RAW;
                default:
                    throw new ArgumentException($"The type of this object ({obj.GetType().Name}) is not supported", nameof(obj));
            }
        }
        public static T[] Repeat<T>(this T value, int times) {
            var ret = new T[times];
            for (int i = 0; i < ret.Length; i++)
                ret[i] = value;
            return ret;
        }
        public static string ToExpandedString<T>(this T obj) {
            if (obj == null)
                return "<null>";
            else if (obj is string str)
                return $"\"{str}\"";
            //else if (obj is BigInteger b)
            //    return b.ToString("x");
            else if (obj is IEnumerable arr)
                return "[" + string.Join(", ", arr.Cast<object>().Select(o => o.ToExpandedString())) + "]";
            else
                return obj.ToString();
        }
        public static bool EqualsZero(this object a)
            => a.GetHashCode() == 0;
        public static void Write(this Stream stream, byte[] buffer)
            => stream.Write(buffer, 0, buffer.Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe IntPtr AddressOf<T>(T t) {
            //refember ReferenceTypes are references to the CLRHeader
            //where TOriginal : struct
            TypedReference reference = __makeref(t);
            return *(IntPtr*)(&reference);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe IntPtr AddressOfRef<T>(ref T t) {
            //refember ReferenceTypes are references to the CLRHeader
            //where TOriginal : struct
            TypedReference reference = __makeref(t);
            TypedReference* pRef = &reference;
            return (IntPtr)pRef; //(&pRef)
        }
        
        //So cool, so powerful, so fast, so unstable, so useful
        public static unsafe T[] ReadBytesAsArrayOf<T>(this byte[] raw, int startIndex = 0, int maxOutputElements = -1) {
            int sizeT = Marshal.SizeOf(typeof(T));
            int actualRead = (raw.Length - startIndex) / sizeT;
            actualRead = Math.Max(actualRead, 0);
            if (maxOutputElements > -1)
                actualRead = Math.Min(actualRead, maxOutputElements);
            var values = new T[actualRead];
            if (actualRead < 1)
                return values;
            fixed (byte* praw = &raw[0]) {
                for (int i = 0; i < actualRead; i++) {
                    var offset = i * sizeT;
                    values[i] = Marshal.PtrToStructure<T>(new IntPtr(praw + offset));
                }
            }
            return values;
        }
        public static T ReadBytesAs<T>(this byte[] raw, int startIndex = 0)
            => ReadBytesAsArrayOf<T>(raw, startIndex, 1)[0];

        public static unsafe byte[] ReadObjectsAsByteArray(this object[] values, int fromIndex = 0, int maxInputElements = -1) {
            if (values.Length < 1)
                throw new ArgumentException("At least one value is required.", nameof(values));
            Type[] types = values.Select(v => v.GetType()).Distinct().ToArray();
            if (types.Length != 1)
                throw new ArgumentException("The values must be of the same type.", nameof(values));
            int sizeT = Marshal.SizeOf(types[0]);
            int actualRead = (values.Length - fromIndex);
            actualRead = Math.Max(actualRead, 0);
            if (maxInputElements > -1)
                actualRead = Math.Min(actualRead, maxInputElements);
            byte[] raw = new byte[actualRead * sizeT];
            if (actualRead < 1)
                return raw;
            fixed (byte* praw = &raw[0]) {
                for (int i = 0; i < actualRead; i++) {
                    int offset = i * sizeT;
                    Marshal.StructureToPtr(values[i], new IntPtr(praw + offset), false);
                }
            }
            return raw;
        }
        public static byte[] ReadObjectAsByteArray(this object value, int startIndex = 0)
            => ReadObjectsAsByteArray(new object[] { value }, startIndex, 1);

        public static unsafe void WriteObjectsInByteArray(this object[] values, byte[] raw, int fromIndexOut = 0, int fromIndexIn = 0, int maxInputElements = -1) {
            if (values.Length < 1)
                throw new ArgumentException("At least one value is required.", nameof(values));
            var types = values.Select(v => v.GetType()).Distinct().ToArray();
            if (types.Length != 1)
                throw new ArgumentException("The values must be of the same type.", nameof(values));
            int sizeT = Marshal.SizeOf(types[0]);
            int actualRead = (values.Length - fromIndexIn);
            actualRead = Math.Max(actualRead, 0);
            if (maxInputElements > -1)
                actualRead = Math.Min(actualRead, maxInputElements);
            if (actualRead < 1)
                return;
            fixed (byte* praw = &raw[0]) {
                for (int i = 0; i < actualRead; i++) {
                    var offset2 = fromIndexOut + i * sizeT;
                    if (offset2 + sizeT <= raw.Length)
                        Marshal.StructureToPtr(values[i], new IntPtr(praw + fromIndexOut), false);
                }
            }
        }
        public static void WriteObjectInByteArray(this object value, byte[] raw, int fromIndexOut = 0, int fromIndexIn = 0)
            => WriteObjectsInByteArray(new object[] { value }, raw, fromIndexOut, fromIndexIn, 1);

        public static void BreakDebugger(string message) {
            DebugWriteLine(message);
            //if (Debugger.IsAttached)
            //    Debugger.Break();
        }
    }
}
