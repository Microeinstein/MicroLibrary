using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using static Micro.Core;

namespace Micro.IO {
    public abstract class CommandHandler : IDisposable {
        public ProtocolRules Rules {
            get => _rules;
            set {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));
                _rules = value;
            }
        }
        protected Stream stream;
        ProtocolRules _rules;
        protected object lockTarget = new object();

        public CommandHandler(Stream readFrom, ProtocolRules rules) {
            stream = readFrom ?? throw new ArgumentNullException(nameof(readFrom));
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }
        public abstract void Dispose();
        int nextNonSpecial(CommandModel model, int paramIndex) {
            bool curr = false,
                 prev = false;
            for (int i = paramIndex; i < model.Count; i++) {
                curr = !model[i].HasFlag(ParamType._SPECIAL);
                if (curr && prev)
                    return i;
                prev = curr;
            }
            return model.Count;
        }
    }
    
    public class CommandReader : CommandHandler {
        public bool IgnoreInitialGarbage { get; set; } = false;
        protected BinaryReader reader;

        public CommandReader(Stream readFrom, ProtocolRules rules) : base(readFrom, rules) {
            reader = new BinaryReader(readFrom, rules.stringEncoding, true);
        }
        public override void Dispose() {
            stream = null;
            reader.Dispose();
        }
        public ResultType TryRead(out Command command) {
            if (!stream.CanRead)
                throw new InvalidOperationException("The stream is unreadable.");
            
            var readArgs = new List<object>();
            object prevObj = null, currObj;
            int arrayIndex = 0,
                arrayLength = 0;
            Array readingArray = null;

            lock (lockTarget) {
                #region COMMAND_START
                awaitForStart:
                int wrongBytes = 0;
                if (reader.TryReadByte(out byte startByte) != null) {
                    BreakDebugger("CANT_READ: start byte");
                    goto cantRead;
                }
                if (startByte != Rules.commandStart) {
                    if (startByte == byte.MaxValue)
                        goto terminated;
                    else {
                        wrongBytes++;
                        goto awaitForStart;
                    }
                }
                wrongBytes = 0;
                #endregion

                #region COMMAND_ID
                if (reader.TryReadUInt16(out ushort cmdID) != null) {
                    BreakDebugger("CANT_READ: command id");
                    goto cantRead;
                }
                if (!Rules.TryGetModel(cmdID, out var cmdModel)) {
                    BreakDebugger("UNK_CMD: command model");
                    goto unknownCommand;
                }
                #endregion

                #region CONTENT
                for (int iarg = 0, iparam = 0; iparam < cmdModel.Count; iparam++) {
                    ParamType param = cmdModel[iparam];
                    #region _SET_OBJECTS
                    if (readingArray != null) {
                        currObj = readingArray.GetValue(arrayIndex);
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
                            if (readingArray != null)
                                throw new InvalidOperationException(nameof(ParamType._ARRAY_OF) + " cannot be repeated.");
                            else {
                                #region ARRAY_LENGTH
                                if (reader.TryReadInt32(out arrayLength) != null) {
                                    BreakDebugger("CANT_READ: array length");
                                    goto cantRead;
                                }
                                if (arrayLength < 0) {
                                    BreakDebugger("INV_DATA: array length < 0");
                                    goto invalidData;
                                }
                                #endregion
                                readingArray = new object[arrayLength];
                                readArgs.Add(readingArray);
                                arrayIndex = 0;
                                if (arrayLength == 0) {
                                    readingArray = null;
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
                            if (reader.TryReadByte(out byte nextByte) != null) {
                                BreakDebugger("CANT_READ: next byte");
                                goto cantRead;
                            }
                            if (nextByte != Rules.commandNext) {
                                BreakDebugger("INV_DATA: next byte");
                                goto invalidData;
                            }
                        }
                        #endregion

                        #region _DYNAMIC
                        if (param == ParamType.DYNAMIC) {
                            if (reader.TryReadUInt16(out ushort dynTypeID) != null) {
                                BreakDebugger("CANT_READ: param type");
                                goto cantRead;
                            }
                            ParamType dynType = (ParamType)dynTypeID;
                            if ((dynType & ParamType.__DATATYPES) == 0) {
                                BreakDebugger("INV_DATA: param type (this is suspicious)");
                                goto invalidData;
                            }
                            #region COMMAND_DEFINE
                            if (reader.TryReadByte(out byte defineByte) != null) {
                                BreakDebugger("CANT_READ: define byte");
                                goto cantRead;
                            }
                            if (defineByte != Rules.commandDefine) {
                                BreakDebugger("INV_DATA: define byte");
                                goto invalidData;
                            }
                            #endregion
                            var kind = (dynType & ParamType.__KINDS);
                            int res = 0;
                            if (kind == ParamType._FIXED_LENGTH)
                                res = tryReadFixed(dynType);
                            else if (kind == ParamType._PREFIXED_LENGTH)
                                res = tryReadPrefixed(dynType);
                            if (res == 1) {
                                BreakDebugger("CANT_READ: dynamic content");
                                goto cantRead;
                            } else if (res == 2) {
                                BreakDebugger("INV_DATA: dynamic content");
                                goto invalidData;
                            }
                            iarg++;
                        }
                        #endregion

                        #region _FIXED_LENGTH
                        else if (param.HasFlag(ParamType._FIXED_LENGTH)) {
                            int res = tryReadFixed(param);
                            if (res == 1) {
                                BreakDebugger("CANT_READ: fixed content");
                                goto cantRead;
                            } else if (res == 2) {
                                BreakDebugger("INV_DATA: fixed content");
                                goto invalidData;
                            }
                            iarg++;
                        }
                        #endregion

                        #region _PREFIXED_LENGTH
                        else if (param.HasFlag(ParamType._PREFIXED_LENGTH)) {
                            int res = tryReadPrefixed(param);
                            if (res == 1) {
                                BreakDebugger("CANT_READ: prefixed content");
                                goto cantRead;
                            } else if (res == 2) {
                                BreakDebugger("INV_DATA: prefixed content");
                                goto invalidData;
                            }
                            iarg++;
                        }
                        #endregion
                    }

                    #region _ARRAY_INDEX_CHANGE
                    if (readingArray != null) {
                        if (++arrayIndex == arrayLength)
                            readingArray = null;
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
                if (reader.TryReadByte(out byte endByte) != null) {
                    BreakDebugger("CANT_READ: end byte");
                    goto cantRead;
                }
                if (endByte != Rules.commandEnd) {
                    BreakDebugger("INV_DATA: end byte");
                    goto invalidData;
                }
                #endregion

                command = new Command(cmdModel, readArgs.ToArray());
                return ResultType.SUCCESS;

                invalidData:
                cleanUp();
                command = new Command();
                return ResultType.INVALID_DATA;

                terminated:
                cleanUp();
                command = new Command();
                return ResultType.TERMINATED;

                cantRead:
                cleanUp();
                command = new Command();
                return ResultType.CANT_READ;

                unknownCommand:
                cleanUp();
                command = new Command();
                return ResultType.UNKNOWN_COMMAND;
            }

            //--------------------------------
            int tryReadFixed(ParamType param) {
                Exception exc = null;
                object arg = null;
                switch (param) {
                    case ParamType.BYTES_1:
                        exc = reader.TryReadBytes(1, out var by1);
                        arg = by1;
                        break;
                    case ParamType.BYTES_2:
                        exc = reader.TryReadBytes(2, out var by2);
                        arg = by2;
                        break;
                    case ParamType.BYTES_4:
                        exc = reader.TryReadBytes(4, out var by4);
                        arg = by4;
                        break;
                    case ParamType.BYTES_8:
                        exc = reader.TryReadBytes(8, out var by8);
                        arg = by8;
                        break;
                    case ParamType.GUID:
                    case ParamType.BYTES_16:
                        exc = reader.TryReadBytes(16, out var by16);
                        arg = by16;
                        break;

                    case ParamType.BOOLEAN:
                        exc = reader.TryReadBoolean(out var bo);
                        arg = bo;
                        break;
                    case ParamType.CHAR:
                        exc = reader.TryReadChar(out var ch);
                        arg = ch;
                        break;
                    case ParamType.SBYTE:
                        exc = reader.TryReadSByte(out var sb);
                        arg = sb;
                        break;
                    case ParamType.BYTE:
                        exc = reader.TryReadByte(out var by);
                        arg = by;
                        break;
                    case ParamType.SHORT:
                        exc = reader.TryReadInt16(out var sh);
                        arg = sh;
                        break;
                    case ParamType.USHORT:
                        exc = reader.TryReadUInt16(out var us);
                        arg = us;
                        break;
                    case ParamType.INT:
                        exc = reader.TryReadInt32(out var ii);
                        arg = ii;
                        break;
                    case ParamType.UINT:
                        exc = reader.TryReadUInt32(out var ui);
                        arg = ui;
                        break;
                    case ParamType.LONG:
                    case ParamType.DATETIME:
                        exc = reader.TryReadInt64(out var ll);
                        arg = ll;
                        break;
                    case ParamType.ULONG:
                        exc = reader.TryReadUInt64(out var ul);
                        arg = ul;
                        break;
                    case ParamType.FLOAT:
                        exc = reader.TryReadSingle(out var ff);
                        arg = ff;
                        break;
                    case ParamType.DOUBLE:
                        exc = reader.TryReadDouble(out var dd);
                        arg = dd;
                        break;
                    case ParamType.DECIMAL:
                        exc = reader.TryReadDecimal(out var de);
                        arg = de;
                        break;
                }
                if (exc != null)
                    return 1;
                switch (param) {
                    case ParamType.DATETIME:
                        arg = DateTime.FromBinary((long)arg);
                        break;
                    case ParamType.GUID:
                        arg = new Guid((byte[])arg);
                        break;
                }
                appendObject(arg);
                return 0;
            }
            int tryReadPrefixed(ParamType param) {
                int length = 0;
                if (reader.TryReadInt32(out length) != null)
                    return 1;
                #region COMMAND_DEFINE
                if (reader.TryReadByte(out byte defineByte) != null)
                    return 1;
                if (defineByte != Rules.commandDefine)
                    return 2;
                #endregion
                if (reader.TryReadBytes(length, out byte[] rawData) != null)
                    return 1;
                object arg = null;
                switch (param) {
                    case ParamType.STRING:
                        arg = Rules.stringEncoding.GetString(rawData);
                        break;
                    //case ParamType.BIGINTEGER:
                    //    arg = new BigInteger(rawData);
                    //    break;
                    case ParamType.RAW:
                        arg = rawData;
                        break;
                }
                appendObject(arg);
                return 0;
            }
            void appendObject(object obj) {
                if (readingArray != null)
                    readingArray.SetValue(obj, arrayIndex);
                else
                    readArgs.Add(obj);
            }
            void cleanUp() {
                prevObj = null;
                currObj = null;
                readingArray = null;
                arrayIndex = arrayLength = 0;
            }
        }
    }
    
    public class CommandWriter : CommandHandler {
        const int ARRAYLENGTH_PLUS_DEFINE = sizeof(int) + 1;
        protected BinaryWriter writer;

        public CommandWriter(Stream writeTo, ProtocolRules rules) : base(writeTo, rules) {
            writer = new BinaryWriter(writeTo, rules.stringEncoding, true);
        }
        public override void Dispose() {
            stream = null;
            writer.Dispose();
        }
        public void TryWrite(in Command command) {
            if (!stream.CanWrite)
                throw new InvalidOperationException("The stream is unwritable.");

            object prevObj = null, currObj;
            int arrayIndex = 0,
                arrayLength = 0;
            Array array = null;

            lock (lockTarget) {
                #region COMMAND_START + COMMAND_ID
                byte[] init = new byte[3];
                init[0] = Rules.commandStart;
                if (!Rules.TryGetModel(command, out _))
                    throw new NotSupportedException("The model of this command is not supported by this class.");
                command.Format.Type.WriteObjectInByteArray(init, 1);
                writer.Write(init);
                #endregion

                #region CONTENT
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
                                writer.Write(arrayLength);
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
                            writer.Write(Rules.commandNext);
                        #endregion

                        #region _DYNAMIC
                        if (param == ParamType.DYNAMIC) {
                            ParamType dynType = FindType(currObj);
                            writer.Write((ushort)dynType);
                            writer.Write(Rules.commandDefine);
                            if (dynType.HasFlag(ParamType._FIXED_LENGTH))
                                tryWriteFixed(dynType, currObj);
                            else if (dynType.HasFlag(ParamType._PREFIXED_LENGTH))
                                tryWritePrefixed(dynType, currObj);
                            else
                                throw new ArgumentException($"Unrecognized type of object ({currObj.GetType().FullName})", nameof(currObj));
                            iarg++;
                        }
                        #endregion

                        #region _FIXED_LENGTH
                        else if (param.HasFlag(ParamType._FIXED_LENGTH)) {
                            tryWriteFixed(param, currObj);
                            iarg++;
                        }
                        #endregion

                        #region _PREFIXED_LENGTH
                        else if (param.HasFlag(ParamType._PREFIXED_LENGTH)) {
                            tryWritePrefixed(param, currObj);
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
                writer.Write(Rules.commandEnd);
                #endregion
                writer.Flush();
            }

            //--------------------------------
            void tryWriteFixed(ParamType param, object value) {
                switch (param) {
                    case ParamType.BYTES_1:
                        writer.Write((byte[])value, 0, 1);
                        break;
                    case ParamType.BYTES_2:
                        writer.Write((byte[])value, 0, 2);
                        break;
                    case ParamType.BYTES_4:
                        writer.Write((byte[])value, 0, 4);
                        break;
                    case ParamType.BYTES_8:
                        writer.Write((byte[])value, 0, 8);
                        break;
                    case ParamType.BYTES_16:
                        writer.Write((byte[])value, 0, 16);
                        break;

                    case ParamType.BOOLEAN:
                        writer.Write((bool)value);
                        break;
                    case ParamType.CHAR:
                        writer.Write((char)value);
                        break;
                    case ParamType.SBYTE:
                        writer.Write((sbyte)value);
                        break;
                    case ParamType.BYTE:
                        writer.Write((byte)value);
                        break;
                    case ParamType.SHORT:
                        writer.Write((short)value);
                        break;
                    case ParamType.USHORT:
                        writer.Write((ushort)value);
                        break;
                    case ParamType.INT:
                        writer.Write((int)value);
                        break;
                    case ParamType.UINT:
                        writer.Write((uint)value);
                        break;
                    case ParamType.LONG:
                        writer.Write((long)value);
                        break;
                    case ParamType.ULONG:
                        writer.Write((ulong)value);
                        break;
                    case ParamType.FLOAT:
                        writer.Write((float)value);
                        break;
                    case ParamType.DOUBLE:
                        writer.Write((double)value);
                        break;
                    case ParamType.DECIMAL:
                        writer.Write((decimal)value);
                        break;
                    case ParamType.DATETIME:
                        writer.Write(((DateTime)value).ToBinary());
                        break;
                    case ParamType.GUID:
                        writer.Write(((Guid)value).ToByteArray());
                        break;
                }
            }
            void tryWritePrefixed(ParamType param, object value) {
                //LENGTH;DATA
                byte[] data = null;
                if (value is string s)
                    data = Rules.stringEncoding.GetBytes(s);
                //else if (value is BigInteger n)
                //    data = n.ToByteArray();
                else if (value is byte[] b)
                    data = b;
                else
                    throw new ArgumentException("Invalid type of data (only string and byte[] are allowed).", nameof(value));
                writer.Write(data.Length);
                writer.Write(Rules.commandDefine);
                writer.Write(data);
            }
        }
    }
}
