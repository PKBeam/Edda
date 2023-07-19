using System;

namespace Edda {
    public class Bookmark : IComparable, IEquatable<Bookmark>
    {

        public double beat { get; set; }
        public string name { get; set; }
        public Bookmark(double beat, string name) {
            this.beat = beat;
            this.name = name;
        }

        public int CompareTo(object obj)
        {
            if (!(obj is Bookmark b))
            {
                throw new Exception();
            }
            Bookmark that = this;
            if (that.Equals(b))
            {
                return 0;
            }
            if (Helper.DoubleApproxGreater(that.beat, b.beat))
            {
                return 1;
            }
            return -1;
        }

        public bool Equals(Bookmark b)
        {
            return Helper.DoubleApproxEqual(b.beat, this.beat);
        }
    }
}
