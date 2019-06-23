using System;
using System.IO;

namespace Micro.IO {
    public static partial class Core {
        #region Reading
        public static Exception TryReadBoolean(this BinaryReader reader, out bool value) {
            try {
                value = reader.ReadBoolean();
            } catch (Exception ex) {
                value = default;
                return ex;
            }
            return null;
        }
        public static Exception TryReadByte(this BinaryReader reader, out byte value) {
            try {
                value = reader.ReadByte();
            } catch (Exception ex) {
                value = default;
                return ex;
            }
            return null;
        }
        public static Exception TryReadSByte(this BinaryReader reader, out sbyte value) {
            try {
                value = reader.ReadSByte();
            } catch (Exception ex) {
                value = default;
                return ex;
            }
            return null;
        }
        public static Exception TryReadInt16(this BinaryReader reader, out short value) {
            try {
                value = reader.ReadInt16();
            } catch (Exception ex) {
                value = default;
                return ex;
            }
            return null;
        }
        public static Exception TryReadUInt16(this BinaryReader reader, out ushort value) {
            try {
                value = reader.ReadUInt16();
            } catch (Exception ex) {
                value = default;
                return ex;
            }
            return null;
        }
        public static Exception TryReadInt32(this BinaryReader reader, out int value) {
            try {
                value = reader.ReadInt32();
            } catch (Exception ex) {
                value = default;
                return ex;
            }
            return null;
        }
        public static Exception TryReadUInt32(this BinaryReader reader, out uint value) {
            try {
                value = reader.ReadUInt32();
            } catch (Exception ex) {
                value = default;
                return ex;
            }
            return null;
        }
        public static Exception TryReadInt64(this BinaryReader reader, out long value) {
            try {
                value = reader.ReadInt64();
            } catch (Exception ex) {
                value = default;
                return ex;
            }
            return null;
        }
        public static Exception TryReadUInt64(this BinaryReader reader, out ulong value) {
            try {
                value = reader.ReadUInt64();
            } catch (Exception ex) {
                value = default;
                return ex;
            }
            return null;
        }
        public static Exception TryReadDecimal(this BinaryReader reader, out decimal value) {
            try {
                value = reader.ReadDecimal();
            } catch (Exception ex) {
                value = default;
                return ex;
            }
            return null;
        }
        public static Exception TryReadSingle(this BinaryReader reader, out float value) {
            try {
                value = reader.ReadSingle();
            } catch (Exception ex) {
                value = default;
                return ex;
            }
            return null;
        }
        public static Exception TryReadDouble(this BinaryReader reader, out double value) {
            try {
                value = reader.ReadDouble();
            } catch (Exception ex) {
                value = default;
                return ex;
            }
            return null;
        }
        public static Exception TryReadChar(this BinaryReader reader, out char value) {
            try {
                value = reader.ReadChar();
            } catch (Exception ex) {
                value = default;
                return ex;
            }
            return null;
        }
        public static Exception TryReadString(this BinaryReader reader, out string value) {
            try {
                value = reader.ReadString();
            } catch (Exception ex) {
                value = default;
                return ex;
            }
            return null;
        }
        public static Exception TryReadBytes(this BinaryReader reader, in int count, out byte[] value) {
            try {
                value = reader.ReadBytes(count);
            } catch (Exception ex) {
                value = default;
                return ex;
            }
            return null;
        }
        public static Exception TryReadChars(this BinaryReader reader, in int count, out char[] value) {
            try {
                value = reader.ReadChars(count);
            } catch (Exception ex) {
                value = default;
                return ex;
            }
            return null;
        }
        #endregion

        #region Writing
        public static Exception TryWrite(this BinaryWriter writer, in bool value) {
            try {
                writer.Write(value);
            } catch (Exception ex) {
                return ex;
            }
            return null;
        }
        public static Exception TryWrite(this BinaryWriter writer, in byte value) {
            try {
                writer.Write(value);
            } catch (Exception ex) {
                return ex;
            }
            return null;
        }
        public static Exception TryWrite(this BinaryWriter writer, in sbyte value) {
            try {
                writer.Write(value);
            } catch (Exception ex) {
                return ex;
            }
            return null;
        }
        public static Exception TryWrite(this BinaryWriter writer, in short value) {
            try {
                writer.Write(value);
            } catch (Exception ex) {
                return ex;
            }
            return null;
        }
        public static Exception TryWrite(this BinaryWriter writer, in ushort value) {
            try {
                writer.Write(value);
            } catch (Exception ex) {
                return ex;
            }
            return null;
        }
        public static Exception TryWrite(this BinaryWriter writer, in int value) {
            try {
                writer.Write(value);
            } catch (Exception ex) {
                return ex;
            }
            return null;
        }
        public static Exception TryWrite(this BinaryWriter writer, in uint value) {
            try {
                writer.Write(value);
            } catch (Exception ex) {
                return ex;
            }
            return null;
        }
        public static Exception TryWrite(this BinaryWriter writer, in long value) {
            try {
                writer.Write(value);
            } catch (Exception ex) {
                return ex;
            }
            return null;
        }
        public static Exception TryWrite(this BinaryWriter writer, in ulong value) {
            try {
                writer.Write(value);
            } catch (Exception ex) {
                return ex;
            }
            return null;
        }
        public static Exception TryWrite(this BinaryWriter writer, in decimal value) {
            try {
                writer.Write(value);
            } catch (Exception ex) {
                return ex;
            }
            return null;
        }
        public static Exception TryWrite(this BinaryWriter writer, in float value) {
            try {
                writer.Write(value);
            } catch (Exception ex) {
                return ex;
            }
            return null;
        }
        public static Exception TryWrite(this BinaryWriter writer, in double value) {
            try {
                writer.Write(value);
            } catch (Exception ex) {
                return ex;
            }
            return null;
        }
        public static Exception TryWrite(this BinaryWriter writer, in char value) {
            try {
                writer.Write(value);
            } catch (Exception ex) {
                return ex;
            }
            return null;
        }
        public static Exception TryWrite(this BinaryWriter writer, in string value) {
            try {
                writer.Write(value);
            } catch (Exception ex) {
                return ex;
            }
            return null;
        }
        public static Exception TryWrite(this BinaryWriter writer,  byte[] value) {
            try {
                writer.Write(value);
            } catch (Exception ex) {
                return ex;
            }
            return null;
        }
        public static Exception TryWrite(this BinaryWriter writer,  char[] value) {
            try {
                writer.Write(value);
            } catch (Exception ex) {
                return ex;
            }
            return null;
        }
        public static Exception TryWrite(this BinaryWriter writer, byte[] value, int index, int count) {
            try {
                writer.Write(value, index, count);
            } catch (Exception ex) {
                return ex;
            }
            return null;
        }
        public static Exception TryWrite(this BinaryWriter writer, char[] value, int index, int count) {
            try {
                writer.Write(value, index, count);
            } catch (Exception ex) {
                return ex;
            }
            return null;
        }
        #endregion
    }
}
