using System;
using System.Collections;
using System.Collections.Generic;

namespace Micro.IO {
    /// <summary>
    /// Note: To access the parameters use the indexer property or foreach.
    /// </summary>
    public readonly struct CommandModel : IEnumerable<ParamType>, IReadOnlyList<ParamType>, IEquatable<CommandModel> {
        public readonly ushort Type;
        public int Count
            => Params.Length;
        readonly ParamType[] Params;
        public readonly Type enumType;

        public ParamType this[int i]
            => Params[i];

        public CommandModel(in ushort type, params ParamType[] @params) {
            enumType = null;
            Type = type;
            Params = @params;
        }
        public CommandModel(Enum type, params ParamType[] @params) {
            enumType = type.GetType();
            Type = Convert.ToUInt16(type);
            Params = @params;
        }

        public int GetNextNonSpecialParam(int paramIndex) {
            bool curr = false,
                 prev = false;
            for (int i = paramIndex; i < Params.Length; i++) {
                curr = !Params[i].HasFlag(ParamType._SPECIAL);
                if (curr && prev)
                    return i;
                prev = curr;
            }
            return Params.Length;
        }
        public IEnumerator<ParamType> GetEnumerator()
            => ((IEnumerable<ParamType>)Params).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator()
            => ((IEnumerable<ParamType>)Params).GetEnumerator();
        public override string ToString()
            => (enumType != null ? Enum.GetName(enumType, Type) : (object)Type) + ": " + string.Join(",", Params);
        public override int GetHashCode()
            => base.GetHashCode();
        public override bool Equals(object obj)
            => obj is CommandModel && Equals((CommandModel)obj);
        public bool Equals(CommandModel other)
            => Type == other.Type && Count == other.Count && EqualityComparer<ParamType[]>.Default.Equals(Params, other.Params);

        public static bool operator ==(CommandModel a, CommandModel b)
            => a.Type == b.Type;
        public static bool operator !=(CommandModel a, CommandModel b)
            => a.Type == b.Type;
        public static bool operator ==(CommandModel a, ushort b)
            => a.Type == b;
        public static bool operator !=(CommandModel a, ushort b)
            => a.Type == b;
        public static bool operator ==(CommandModel a, Enum b)
            => a.Type == Convert.ToInt16(b);
        public static bool operator !=(CommandModel a, Enum b)
            => a.Type == Convert.ToInt16(b);
        public static implicit operator ushort(in CommandModel c)
             => c.Type;
    }

    public readonly struct Command {
        public readonly CommandModel Format;
        public readonly object[] Args;

        public Command(in CommandModel f) {
            Format = f;
            Args = new object[0];
        }
        public Command(in CommandModel f, object arg1) {
            Format = f;
            Args = new object[] { arg1 };
        }
        public Command(in CommandModel f, object arg1, object arg2) {
            Format = f;
            Args = new object[] { arg1, arg2 };
        }
        public Command(in CommandModel f, object arg1, object arg2, object arg3) {
            Format = f;
            Args = new object[] { arg1, arg2, arg3 };
        }
        public Command(in CommandModel f, object[] args) {
            Format = f;
            Args = args;
        }
        public override string ToString()
            => Format.Type + ": " + Args.ToExpandedString();
        public static implicit operator ushort(in Command c)
            => c.Format.Type;
    }
}
