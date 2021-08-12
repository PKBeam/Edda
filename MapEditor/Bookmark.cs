using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Edda {
    public class Bookmark {

        public double beat { get; set; }
        public string name { get; set; }
        public Bookmark(double beat, string name) {
            this.beat = beat;
            this.name = name;
        }
    }
}
