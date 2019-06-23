using System;

namespace Micro.IO {
    [Flags]
    public enum ParamType : ushort {
        //0000000000000___
        _FIXED_LENGTH    = 01 << 00,
        _PREFIXED_LENGTH = 01 << 01,
        _SPECIAL         = 01 << 02,
        
        //0000000000___000
        BYTES_1  = 01 << 03 | _FIXED_LENGTH,
        BYTES_2  = 02 << 03 | _FIXED_LENGTH,
        BYTES_4  = 03 << 03 | _FIXED_LENGTH,
        BYTES_8  = 04 << 03 | _FIXED_LENGTH,
        BYTES_16 = 05 << 03 | _FIXED_LENGTH,

        //00000_____000000
        DYNAMIC    = 01 << 06, //Any type of value
        BOOLEAN    = 02 << 06 | BYTES_4,   //BYTES_1,
        CHAR       = 03 << 06 | BYTES_1,   //BYTES_2,
        SBYTE      = 04 << 06 | BYTES_1,   //BYTES_1,
        BYTE       = 05 << 06 | BYTES_1,   //BYTES_1,
        SHORT      = 06 << 06 | BYTES_2,   //BYTES_2,
        USHORT     = 07 << 06 | BYTES_2,   //BYTES_2,
        INT        = 08 << 06 | BYTES_4,   //BYTES_4,
        UINT       = 09 << 06 | BYTES_4,   //BYTES_4,
        LONG       = 10 << 06 | BYTES_8,   //BYTES_8,
        ULONG      = 11 << 06 | BYTES_8,   //BYTES_8,
        FLOAT      = 12 << 06 | BYTES_4,   //BYTES_4,
        DOUBLE     = 13 << 06 | BYTES_8,   //BYTES_8,
        DECIMAL    = 14 << 06 | BYTES_16,  //BYTES_16,
        DATETIME   = 15 << 06 | BYTES_8,   //BYTES_8,
        GUID       = 16 << 06 | BYTES_16,  //BYTES_16,
        STRING     = 17 << 06 | _PREFIXED_LENGTH,
        //BIGINTEGER = 18 << 06 | _PREFIXED_LENGTH,
        RAW        = 19 << 06 | _PREFIXED_LENGTH,

        //0____00000000000
        _IF_TRUE  = 01 << 11 | _SPECIAL, //Single or concat param
        _IF_0     = 02 << 11 | _SPECIAL, // *
        _IF_NOT_0 = 03 << 11 | _SPECIAL, // *
        _ARRAY_OF = 04 << 11 | _SPECIAL, //Single param N times

        //#
        __KINDS             = 0b111,
        __LENGTHS           = 0b111   << 03,
        __DATATYPES         = 0b11111 << 06,
        __SPECIALS          = 0b1111  << 11,
        __LENGTHS_AND_KINDS = 0b111_111
    }

    public enum ResultType : byte {
        SUCCESS,
        UNKNOWN,
        TERMINATED,
        CANT_READ,
        INVALID_DATA,
        UNKNOWN_COMMAND,
        UNEXPECTED_COMMAND
    }
}
