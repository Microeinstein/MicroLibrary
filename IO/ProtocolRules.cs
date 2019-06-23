using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Micro.IO {
    public class ProtocolRules {
        public readonly byte
            commandStart,
            commandNext,
            commandDefine,
            commandEnd;
        public readonly IEnumerable<CommandModel>
            models;
        public readonly Encoding
            stringEncoding;
        readonly ReadOnlyDictionary<ushort, CommandModel>
            indexedModels;

        public CommandModel this[ushort i]
            => indexedModels[i];

        /// <summary>
        /// Define a new format for a CommandStream
        /// </summary>
        /// <param name="models">Command models supported by the protocol</param>
        /// <param name="commandStart">Initial byte inserted before a command</param>
        /// <param name="commandNext">Separator byte inserted between command parameters</param>
        /// <param name="commandDefine">Separator byte inserted in the presence of a multi-byte parameter</param>
        /// <param name="commandEnd">Final byte inserted after a command</param>
        public ProtocolRules(IEnumerable<CommandModel> models)
            : this(models, Encoding.Unicode, '[', ';', '>', ']') { }
        
        /// <summary>
        /// Define a new format for a CommandStream
        /// </summary>
        /// <param name="supported">Command models supported by the protocol</param>
        /// <param name="commandStart">Initial byte inserted before a command</param>
        /// <param name="commandNext">Separator byte inserted between command parameters</param>
        /// <param name="commandDefine">Separator byte inserted in the presence of a multi-byte parameter</param>
        /// <param name="commandEnd">Final byte inserted after a command</param>
        public ProtocolRules(IEnumerable<CommandModel> models, Encoding stringEncoding, in char commandStart, in char commandNext, in char commandDefine, in char commandEnd)
            : this(models, stringEncoding, (byte)commandStart, (byte)commandNext, (byte)commandDefine, (byte)commandEnd) { }

        /// <summary>
        /// Define a new format for a CommandStream
        /// </summary>
        /// <param name="models">Command models supported by the protocol</param>
        /// <param name="commandStart">Initial byte inserted before a command</param>
        /// <param name="commandNext">Separator byte inserted between command parameters</param>
        /// <param name="commandDefine">Separator byte inserted in the presence of a multi-byte parameter</param>
        /// <param name="commandEnd">Final byte inserted after a command</param>
        public ProtocolRules(IEnumerable<CommandModel> models, Encoding stringEncoding, in byte commandStart, in byte commandNext, in byte commandDefine, in byte commandEnd) {
            this.models = models.ToList().AsReadOnly();
            this.indexedModels = new ReadOnlyDictionary<ushort, CommandModel>(models.ToDictionary(m => m.Type));
            this.stringEncoding = stringEncoding ?? throw new ArgumentNullException(nameof(stringEncoding));
            this.commandStart = commandStart;
            this.commandNext = commandNext;
            this.commandDefine = commandDefine;
            this.commandEnd = commandEnd;
        }

        public bool TryGetModel(ushort type, out CommandModel model)
            => indexedModels.TryGetValue(type, out model);
    }
}
