#pragma warning disable CS0809
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using static Micro.Core;

namespace Micro.IO {
    [Obsolete("Please use CommandReader and CommandWriter.")]
    public class CommandStream : Stream {
        const int ARRAYLENGTH_PLUS_DEFINE = sizeof(int) + 1;

        public override bool CanRead
            => UsedStream.CanRead;
        public override bool CanSeek
            => false;
        public override bool CanWrite
            => UsedStream.CanWrite;
        [Obsolete("This class does not support Length", true)]
        public override long Length
            => throw new NotSupportedException();
        [Obsolete("This class does not support Position", true)]
        public override long Position {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public ProtocolRules Rules;
        public Stream UsedStream { get; set; }
        public bool IgnoreInitialGarbage { get; set; } = false;
        List<object> readArgs = new List<object>();
        byte[] readBuffer;
        long readingTasks = 0,
             writingTasks = 0;
        AutoResetEvent readSignal = new AutoResetEvent(false),
                       writeSignal = new AutoResetEvent(false);

        public CommandStream(Stream underlying, ProtocolRules rules, uint bufferSize = 256) {
            UsedStream = underlying;
            readBuffer = new byte[bufferSize];
            Rules = rules;
        }
        protected override void Dispose(bool disposing) {
            readSignal.Dispose();
            writeSignal.Dispose();
        }
        public void CriticalPause() {
            if (Interlocked.Read(ref readingTasks) >= 1) { //Wait in case of a task
                if (!readSignal.WaitOne())
                    return;
            }
            Interlocked.Increment(ref readingTasks);       //Add a fake task
            if (Interlocked.Read(ref writingTasks) >= 1) {
                if (!writeSignal.WaitOne())
                    return;
            }
            Interlocked.Increment(ref writingTasks);
        }
        public void CriticalResume() {
            if (Interlocked.Read(ref readingTasks) >= 1) { //Signal in case of a task
                readSignal.Set();
                Interlocked.Decrement(ref readingTasks);
            }
            if (Interlocked.Read(ref writingTasks) >= 1) {
                writeSignal.Set();
                Interlocked.Decrement(ref writingTasks);
            }
        }

        /// <summary>
        /// Blocca il flusso chiamate corrente finché non riesce a leggere un comando dall'underlyingStream sottostante.
        /// </summary>
        /// <param name="command">Valore restituito</param>
        /// <returns>Tipo di errore nel caso non sia possibile leggere un comando corretto</returns>
        public ResultType ReadCommand(out Command command) {
            if (!UsedStream.CanRead)
                throw new InvalidOperationException("The underlyingStream is unwritable.");

            command = default;
            if (Interlocked.Read(ref readingTasks) >= 1) {
                if (!readSignal.WaitOne())
                    return ResultType.CANT_READ;
            }
            Interlocked.Increment(ref readingTasks);

            readArgs.Clear();

            #region COMMAND_START
            awaitForStart:
            ushort wrongBytes = 0;
            wrongBytes++;
            if (!readChunk(1))
                goto cantRead;
            byte startByte = readBuffer[0];
            if (startByte != Rules.commandStart) {
                if (startByte == byte.MaxValue)
                    goto terminated;
                else if (IgnoreInitialGarbage || startByte == byte.MinValue)
                    goto awaitForStart;
                else
                    goto invalidData;
            }
            wrongBytes--;
            //Debug.WriteLineIf(wrongBytes > 0, "WrongBytes: " + wrongBytes);
            #endregion

            #region COMMAND_ID
            if (!readStruct(out ushort cmdID))
                goto cantRead;
            if (!Rules.TryGetModel(cmdID, out var cmdModel))
                goto unknownCommand;
            #endregion

            #region CONTENT
            bool readOk = false;
            object prevObj = null, currObj;
            int arrayIndex = 0,
                arrayLength = 0;
            Array array = null;
            for (int iarg = 0, iparam = 0; iparam < cmdModel.Count; iparam++) {
                ParamType param = cmdModel[iparam];
                #region _SET_OBJECTS
                if (array != null) {
                    currObj = array.GetValue(arrayIndex);
                } else {
                    currObj = iarg < readArgs.Count ? readArgs[iarg] : null;
                    prevObj = iarg == 0 ? null : readArgs[iarg - 1];
                }
                #endregion

                if (param.HasFlag(ParamType._SPECIAL)) {
                    #region _IF_TRUE
                    if (param == ParamType._IF_TRUE) {
                        if (iarg == 0 || !(prevObj is bool))
                            throw new InvalidOperationException(nameof(ParamType._IF_TRUE) + " requires a missing parameter.");
                        else if (!(bool)prevObj)
                            iparam = cmdModel.GetNextNonSpecialParam(iparam);    //Skip next non-special parameters
                        continue;
                    }
                    #endregion

                    #region _IF_0
                    if (param == ParamType._IF_0) {
                        if (iarg == 0)
                            throw new InvalidOperationException(nameof(ParamType._IF_0) + " requires a missing parameter.");
                        else if (!prevObj.EqualsZero())
                            iparam = cmdModel.GetNextNonSpecialParam(iparam);    //Skip next non-special parameter
                        continue;
                    }
                    #endregion

                    #region _IF_NOT_0
                    else if (param == ParamType._IF_NOT_0) {
                        if (iarg == 0)
                            throw new InvalidOperationException(nameof(ParamType._IF_NOT_0) + " requires a missing parameter.");
                        else if (prevObj.EqualsZero())
                            iparam = cmdModel.GetNextNonSpecialParam(iparam);    //Skip next non-special parameter
                        continue;
                    }
                    #endregion

                    #region _ARRAY_OF
                    else if (param == ParamType._ARRAY_OF) {
                        if (iarg == 0)
                            throw new InvalidOperationException(nameof(ParamType._ARRAY_OF) + " require a missing parameter.");
                        else if (array != null)
                            throw new InvalidOperationException(nameof(ParamType._ARRAY_OF) + " cannot be repeated.");
                        else {
                            #region ARRAY_LENGTH
                            if (!readStruct(out arrayLength))
                                goto cantRead;
                            if (arrayLength < 0)
                                goto invalidData;
                            #endregion
                            array = new object[arrayLength];
                            readArgs.Add(array);
                            arrayIndex = 0;
                            if (arrayLength == 0) {
                                array = null;
                                iparam++;
                            }
                        }
                        continue;
                    }
                    #endregion

                    #region <ERROR>
                    else
                        throw new InvalidOperationException("Unable to recognize special parameter: " + $"0x{param:x}");
                    #endregion
                } else {
                    #region COMMAND_NEXT
                    if (iarg > 0) {
                        if (!readChunk(1))
                            goto cantRead;
                        if (readBuffer[0] != Rules.commandNext)
                            goto invalidData;
                    }
                    #endregion

                    #region _DYNAMIC
                    if (param == ParamType.DYNAMIC) {
                        if (!readStruct(out ushort dynTypeID))
                            goto cantRead;
                        ParamType dynType = (ParamType)dynTypeID;
                        if ((dynType & ParamType.__DATATYPES) == 0)
                            goto invalidData;
                        #region COMMAND_DEFINE
                        if (!readChunk(1))
                            goto cantRead;
                        if (readBuffer[0] != Rules.commandDefine)
                            goto invalidData;
                        #endregion
                        var kind = (dynType & ParamType.__KINDS);
                        int res = 0;
                        if (kind == ParamType._FIXED_LENGTH)
                            res = readFixed(dynType);
                        else if (kind == ParamType._PREFIXED_LENGTH)
                            res = readPrefixed(dynType);
                        if (res == 1)
                            goto cantRead;
                        else if (res == 2)
                            goto invalidData;
                        iarg++;
                    }
                    #endregion

                    #region _FIXED_LENGTH
                    else if (param.HasFlag(ParamType._FIXED_LENGTH)) {
                        int res = readFixed(param);
                        if (res == 1)
                            goto cantRead;
                        else if (res == 2)
                            goto invalidData;
                        iarg++;
                    }
                    #endregion

                    #region _PREFIXED_LENGTH
                    else if (param.HasFlag(ParamType._PREFIXED_LENGTH)) {
                        int res = readPrefixed(param);
                        if (res == 1)
                            goto cantRead;
                        else if (res == 2)
                            goto invalidData;
                        iarg++;
                    }
                    #endregion
                }

                #region _ARRAY_INDEX_CHANGE
                if (array != null) {
                    if (++arrayIndex == arrayLength)
                        array = null;
                    else {
                        iparam--;
                        iarg--;
                    }
                }
                #endregion
            }
            cleanUp();
            #endregion

            #region COMMAND_END
            if (!readChunk(1))
                goto cantRead;
            if (readBuffer[0] != Rules.commandEnd)
                goto invalidData;
            #endregion

            command = new Command(cmdModel, readArgs.ToArray());
            nextCall();
            return ResultType.SUCCESS;

        invalidData:
            cleanUp();
            nextCall();
            return ResultType.INVALID_DATA;

        terminated:
            cleanUp();
            nextCall();
            return ResultType.TERMINATED;

        cantRead:
            cleanUp();
            nextCall();
            return ResultType.CANT_READ;

        unknownCommand:
            cleanUp();
            nextCall();
            return ResultType.UNKNOWN_COMMAND;

            //--------------------------------
            int readFixed(ParamType param) {
                switch (param & ParamType.__LENGTHS_AND_KINDS) {
                    case ParamType.BYTES_1:
                        readOk = readChunk(1);
                        break;
                    case ParamType.BYTES_2:
                        readOk = readChunk(2);
                        break;
                    case ParamType.BYTES_4:
                        readOk = readChunk(4);
                        break;
                    case ParamType.BYTES_8:
                        readOk = readChunk(8);
                        break;
                    case ParamType.BYTES_16:
                        readOk = readChunk(16);
                        break;
                }
                if (!readOk)
                    return 1;

                object arg = null;
                switch (param) {
                    case ParamType.BYTES_1:
                        arg = readBuffer.ReadBytesAsArrayOf<byte>(maxOutputElements: 1);
                        break;
                    case ParamType.BYTES_2:
                        arg = readBuffer.ReadBytesAsArrayOf<byte>(maxOutputElements: 2);
                        break;
                    case ParamType.BYTES_4:
                        arg = readBuffer.ReadBytesAsArrayOf<byte>(maxOutputElements: 4);
                        break;
                    case ParamType.BYTES_8:
                        arg = readBuffer.ReadBytesAsArrayOf<byte>(maxOutputElements: 8);
                        break;
                    case ParamType.BYTES_16:
                        arg = readBuffer.ReadBytesAsArrayOf<byte>(maxOutputElements: 16);
                        break;

                    case ParamType.BOOLEAN:
                        arg = readBuffer.ReadBytesAs<bool>();
                        break;
                    case ParamType.CHAR:
                        arg = readBuffer.ReadBytesAs<char>();
                        break;
                    case ParamType.SBYTE:
                        arg = readBuffer.ReadBytesAs<sbyte>();
                        break;
                    case ParamType.BYTE:
                        arg = readBuffer.ReadBytesAs<byte>();
                        break;
                    case ParamType.SHORT:
                        arg = readBuffer.ReadBytesAs<short>();
                        break;
                    case ParamType.USHORT:
                        arg = readBuffer.ReadBytesAs<ushort>();
                        break;
                    case ParamType.INT:
                        arg = readBuffer.ReadBytesAs<int>();
                        break;
                    case ParamType.UINT:
                        arg = readBuffer.ReadBytesAs<uint>();
                        break;
                    case ParamType.LONG:
                        arg = readBuffer.ReadBytesAs<long>();
                        break;
                    case ParamType.ULONG:
                        arg = readBuffer.ReadBytesAs<ulong>();
                        break;
                    case ParamType.FLOAT:
                        arg = readBuffer.ReadBytesAs<float>();
                        break;
                    case ParamType.DOUBLE:
                        arg = readBuffer.ReadBytesAs<double>();
                        break;
                    case ParamType.DECIMAL:
                        arg = readBuffer.ReadBytesAs<decimal>();
                        break;

                    case ParamType.DATETIME:
                        arg = DateTime.FromBinary(readBuffer.ReadBytesAs<long>());
                        break;
                    case ParamType.GUID:
                        arg = new Guid(readBuffer.ReadBytesAsArrayOf<byte>(maxOutputElements: 16));
                        break;
                }
                appendObject(arg);
                return 0;
            }
            int readPrefixed(ParamType param) {
                int length = 0;
                if (!readStruct(out length))
                    return 1;
                #region COMMAND_DEFINE
                if (!readChunk(1))
                    return 1;
                if (readBuffer[0] != Rules.commandDefine)
                    return 2;
                #endregion
                if (!readChunk(length))
                    return 1;
                object arg = null;
                switch (param) {
                    case ParamType.STRING:
                        arg = Rules.stringEncoding.GetString(readBuffer, 0, length);
                        break;
                    //case ParamType.BIGINTEGER:
                    //    arg = new BigInteger(readBuffer.ReadBytesAsArrayOf<byte>(0, length));
                    //    break;
                    case ParamType.RAW:
                        arg = readBuffer.ReadBytesAsArrayOf<byte>(0, length);
                        break;
                }
                appendObject(arg);
                return 0;
            }
            void appendObject(object obj) {
                if (array != null) {
                    array.SetValue(obj, arrayIndex);
                } else {
                    readArgs.Add(obj);
                }
            }
            void nextCall() {
                if (Interlocked.Read(ref readingTasks) >= 1) {
                    readSignal.Set();
                    Interlocked.Decrement(ref readingTasks);
                }
            }
            void cleanUp() {
                prevObj = null;
                currObj = null;
                array = null;
                arrayIndex = arrayLength = 0;
            }
        }
        
        /// <summary>
        /// Blocca il flusso chiamate corrente finché non finisce di scrivere un comando sullo underlyingStream sottostante.
        /// </summary>
        /// <param name="command">Valore da scrivere</param>
        public void WriteCommand(Command command) {
            if (!UsedStream.CanWrite)
                throw new InvalidOperationException("The underlyingStream is unwritable.");

            if (Interlocked.Read(ref writingTasks) >= 1) {
                if (!writeSignal.WaitOne())
                    return;
            }
            Interlocked.Increment(ref writingTasks);

            #region COMMAND_START + COMMAND_ID
            byte[] init = new byte[3];
            init[0] = Rules.commandStart;
            if (!Rules.TryGetModel(command, out _))
                throw new NotSupportedException("The model of this command is not supported by this class.");
            command.Format.Type.WriteObjectInByteArray(init, 1);
            UsedStream.Write(init);
            #endregion

            #region CONTENT
            object prevObj = null, currObj;
            int arrayIndex = 0,
                arrayLength = 0;
            Array array = null;
            for (int iarg = 0, iparam = 0; iparam < command.Format.Count; iparam++) {
                ParamType param = command.Format[iparam];
                #region _SET_OBJECTS
                if (array != null) {
                    currObj = array.GetValue(arrayIndex);
                } else {
                    currObj = iarg < command.Args.Length ? command.Args[iarg] : null;
                    prevObj = iarg == 0 ? null : command.Args[iarg - 1];
                }
                #endregion

                if (param.HasFlag(ParamType._SPECIAL)) {
                    #region _IF_TRUE
                    if (param == ParamType._IF_TRUE) {
                        if (prevObj == null || !(prevObj is bool))
                            throw new InvalidOperationException(nameof(ParamType._IF_TRUE) + " requires a missing parameter.");
                        else if (!(bool)prevObj)
                            iparam = command.Format.GetNextNonSpecialParam(iparam);    //Skip next non-special parameter
                        continue;
                    }
                    #endregion

                    #region _IF_0
                    else if (param == ParamType._IF_0) {
                        if (prevObj == null)
                            throw new InvalidOperationException(nameof(ParamType._IF_0) + " requires a missing parameter.");
                        else if (!prevObj.EqualsZero())
                            iparam = command.Format.GetNextNonSpecialParam(iparam);    //Skip next non-special parameter
                        continue;
                    }
                    #endregion

                    #region _IF_NOT_0
                    else if (param == ParamType._IF_NOT_0) {
                        if (prevObj == null)
                            throw new InvalidOperationException(nameof(ParamType._IF_NOT_0) + " requires a missing parameter.");
                        else if (prevObj.EqualsZero())
                            iparam = command.Format.GetNextNonSpecialParam(iparam);    //Skip next non-special parameter
                        continue;
                    }
                    #endregion

                    #region _ARRAY_OF
                    else if (param == ParamType._ARRAY_OF) {
                        if (currObj == null || !(currObj is Array))
                            throw new InvalidOperationException(nameof(ParamType._ARRAY_OF) + " require a missing parameter.");
                        else if (array != null)
                            throw new InvalidOperationException(nameof(ParamType._ARRAY_OF) + " cannot be repeated.");
                        else {
                            array = (Array)currObj;
                            arrayLength = array.Length;
                            UsedStream.Write(arrayLength.ReadObjectAsByteArray());
                            if (arrayLength == 0) {
                                array = null;
                                iparam++;
                            } else
                                arrayIndex = 0;
                        }
                        continue;
                    }
                    #endregion

                    #region <ERROR>
                    else
                        throw new InvalidOperationException("Unable to recognize special parameter: " + $"0x{param:x}");
                    #endregion
                } else {
                    #region COMMAND_NEXT
                    if (iarg > 0)
                        UsedStream.WriteByte(Rules.commandNext);
                    #endregion

                    #region _DYNAMIC
                    if (param == ParamType.DYNAMIC) {
                        ParamType dynType = FindType(currObj);
                        var bType = ((ushort)dynType).ReadObjectAsByteArray();
                        UsedStream.Write(bType);
                        UsedStream.WriteByte(Rules.commandDefine);
                        UsedStream.Write(
                            dynType.HasFlag(ParamType._FIXED_LENGTH) ? bytesFixed(dynType, currObj) :
                            dynType.HasFlag(ParamType._PREFIXED_LENGTH) ? bytesPrefixed(dynType, currObj) :
                            throw new ArgumentException($"Unrecognized type of object ({currObj.GetType().FullName})", nameof(currObj))
                        );
                        iarg++;
                    }
                    #endregion

                    #region _FIXED_LENGTH
                    else if (param.HasFlag(ParamType._FIXED_LENGTH)) {
                        UsedStream.Write(bytesFixed(param, currObj));
                        iarg++;
                    }
                    #endregion

                    #region _PREFIXED_LENGTH
                    else if (param.HasFlag(ParamType._PREFIXED_LENGTH)) {
                        UsedStream.Write(bytesPrefixed(param, currObj));
                        iarg++;
                    }
                    #endregion
                }

                #region _ARRAY_INDEX_CHANGE
                if (array != null) {
                    if (++arrayIndex == arrayLength)
                        array = null;
                    else {
                        iparam--;
                        iarg--;
                    }
                }
                #endregion
            }
            prevObj = null;
            currObj = null;
            array = null;
            arrayIndex = arrayLength = 0;
            #endregion

            #region COMMAND_END
            UsedStream.WriteByte(Rules.commandEnd);
            #endregion
            Flush();

            writeSignal.Set();
            Interlocked.Decrement(ref writingTasks);

            //--------------------------------
            byte[] bytesFixed<T>(ParamType param, T value) {
                byte[] ret;
                if (!param.HasFlag(ParamType._FIXED_LENGTH))
                    throw new ArgumentException("This parameter does not have a fixed length.", nameof(param));
                if (param == ParamType.DATETIME)
                    ret = ((DateTime)(object)value).ToBinary().ReadObjectAsByteArray();
                else if (param == ParamType.GUID)
                    ret = ((Guid)(object)value).ToByteArray();
                else
                    ret = value.ReadObjectAsByteArray();
                return ret;
            }
            byte[] bytesPrefixed(ParamType param, object value) {
                if (!param.HasFlag(ParamType._PREFIXED_LENGTH))
                    throw new ArgumentException("This parameter does not have a dynamic length.", nameof(param));
                byte[] ret = null;
                int length = 0;
                //LENGTH;DATA
                if (value is string s) {
                    length = Rules.stringEncoding.GetByteCount(s);
                    ret = new byte[ARRAYLENGTH_PLUS_DEFINE + length];
                    length.WriteObjectInByteArray(ret);
                    ret[sizeof(int)] = Rules.commandDefine;
                    Rules.stringEncoding.GetBytes(s, 0, s.Length, ret, ARRAYLENGTH_PLUS_DEFINE);
                }
                //else if (value is BigInteger n) {
                //    var bb = n.ToByteArray();
                //    length = bb.Length;
                //    ret = new byte[ARRAYLENGTH_PLUS_DEFINE + length];
                //    length.WriteObjectInByteArray(ret);
                //    ret[sizeof(int)] = Rules.commandDefine;
                //    Array.Copy(bb, 0, ret, ARRAYLENGTH_PLUS_DEFINE, length);
                //}
                else if (value is byte[] b) {
                    length = b.Length;
                    ret = new byte[ARRAYLENGTH_PLUS_DEFINE + length];
                    length.WriteObjectInByteArray(ret);
                    ret[sizeof(int)] = Rules.commandDefine;
                    Array.Copy(b, 0, ret, ARRAYLENGTH_PLUS_DEFINE, length);
                }
                else
                    throw new ArgumentException("Invalid type of data (only string and byte[] are allowed).", nameof(value));
                return ret;
            }
        }
        
        /// <summary>
        /// Blocca il flusso chiamate usando <see langword="Stream.Read"/> e attende la ricezione di una quantità specificata di dati.
        /// </summary>
        bool readChunk(int requiredLength) {
            if (requiredLength < 0)
                throw new ArgumentOutOfRangeException(nameof(requiredLength), "The required amount of data is negative.");
            if (requiredLength == 0) {
                //throw new ArgumentOutOfRangeException(nameof(requiredLength), "The required amount of data cannot be less than one byte.");
                return true;
            }
            if (requiredLength > readBuffer.Length)
                readBuffer = new byte[requiredLength];

            int totalReceived = 0;
            int received = 0;
            do {
                try {
                    received = UsedStream.Read(readBuffer, totalReceived, requiredLength - totalReceived);
                } catch (Exception ex) {
                    Debug.Print(ex.Message);
                    return false;
                }
                if (received == 0)
                    return false;
                totalReceived += received;
            } while (totalReceived < requiredLength);
            if (totalReceived != requiredLength)
                throw new IndexOutOfRangeException("Weird, but the amount of received data does not match the required one.");
            return true;
        }
        bool readStruct<T>(out T value) where T : struct {
            int sizeT = Marshal.SizeOf(typeof(T));
            value = default;
            if (!readChunk(sizeT))
                return false;
            value = readBuffer.ReadBytesAs<T>();
            return true;
        }

        public override void Close() {
            CriticalPause();
            if (UsedStream != null && UsedStream.CanWrite) {
                try {
                    UsedStream.WriteByte(byte.MaxValue);
                    UsedStream.Flush();
                } catch (IOException) { }
                UsedStream.Close();
                UsedStream = null;
            }
        }
        public override void Flush()
            => UsedStream.Flush();
        [Obsolete("This class does not support Seek", true)]
        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();
        [Obsolete("This class does not support SetLength", true)]
        public override void SetLength(long value)
            => throw new NotSupportedException();
        [Obsolete("This class does not support direct Read", true)]
        public override int Read(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
        [Obsolete("This class does not support direct Write", true)]
        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
    }
}