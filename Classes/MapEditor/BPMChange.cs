using System;

namespace Edda.Classes.MapEditorNS {
    [Serializable]
    public class BPMChange(double globalBeat, double BPM, int gridDivision) : IComparable, IEquatable<BPMChange> {
        public double globalBeat = globalBeat;
        public double BPM = BPM;
        public int gridDivision = gridDivision;

        public BPMChange() : this(0, 120, 4) { }

        public int CompareTo(object obj) {
            if (obj is not BPMChange b) {
                throw new Exception();
            }
            if (Helper.DoubleApproxEqual(globalBeat, b.globalBeat)) {
                return 0;
            }
            if (Helper.DoubleApproxGreater(globalBeat, b.globalBeat)) {
                return 1;
            }
            return -1;
        }

        public override bool Equals(object obj) => obj is BPMChange b && Equals(b);
        public override int GetHashCode() => HashCode.Combine(Math.Round(globalBeat, 4), Math.Round(BPM, 4), gridDivision);
        public bool Equals(BPMChange b) => Helper.DoubleApproxEqual(globalBeat, b.globalBeat) && Helper.DoubleApproxEqual(BPM, b.BPM) && gridDivision == b.gridDivision;
    }
}