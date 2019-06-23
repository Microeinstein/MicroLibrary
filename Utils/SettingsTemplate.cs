using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Micro.Utils;

namespace ProjectName {
    public class Classname : Settings {
        const string
            keyItem1 = "Item1",
            keyItem2 = "Item2",
            keyItem3 = "Item3";

        public string Item1 {
            get => base[keyItem1];
            set => base[keyItem1] = value;
        }
        public double? Item2 {
            get => base[keyItem2] == null ? (double?)null : double.Parse(base[keyItem2]);
            set => base[keyItem2] = value + "";
        }
        public bool? Item3 {
            get => base[keyItem3] == null ? (bool?)null : bool.Parse(base[keyItem3]);
            set => base[keyItem3] = value + "";
        }

        public Classname(string path) : base(path, Encoding.ASCII) { }
        public override void LoadDefaults(bool overwrite) {
            if (!overwrite) {
                Item1 = Item1 ?? "Value1";
                Item2 = Item2 ?? Math.PI;
                Item3 = Item3 ?? false;
            } else {
                Item1 = "Value1";
                Item2 = Math.PI;
                Item3 = false;
            }
        }
    }
}

