using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Edda {
    /// <summary>
    /// Interaction logic for BPMCalcWindow.xaml
    /// </summary>
    public partial class BPMCalcWindow : Window {

        Stopwatch stopwatch;
        List<long> intervalSamples;
        int numInputs = 0;
        long prevTime = 0;

        public BPMCalcWindow() {
            InitializeComponent();
            stopwatch = new();
            intervalSamples = new();
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e) {
            // reset variables
            lblAvgBPM.Content = 0;
            lblUnroundedAvgBPM.Content = "(0.00)";
            //lblMedBPM.Content = 0;
            prevTime = 0;
            numInputs = 0;
            lblInputCounter.Content = numInputs;
            intervalSamples.Clear();

            stopwatch.Reset();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e) {
            // start timer
            if (!stopwatch.IsRunning) {
                stopwatch.Start();
            }
            else {

                // count an input
                long now = stopwatch.ElapsedMilliseconds;
                intervalSamples.Add(now - prevTime);

                // increment input counter
                numInputs++;
                lblInputCounter.Content = numInputs;

                prevTime = now;

                // calculate BPM
                CalculateBPM();
            }
        }
        private void CalculateBPM() {
            int numSamples = intervalSamples.Count;

            if (numSamples == 0) {
                return;
            }

            intervalSamples.Sort();

            // calculate mean
            double avgInterval = intervalSamples.Sum() / (double)intervalSamples.Count;

            /*
            // calculate median
            double medInterval;
            if (numSamples % 2 == 1) { // middle element exists
                medInterval = intervalSamples[numSamples / 2];
            } else { // take average of two middle elements
                medInterval = 0.5 * (intervalSamples[numSamples / 2] + intervalSamples[numSamples / 2 - 1]);
            }
            */
            lblUnroundedAvgBPM.Content = "(" + (60000 / avgInterval).ToString("0.00") + ")";
            lblAvgBPM.Content = (60000 / avgInterval).ToString("0.");
            //lblMedBPM.Content = (60000 / medInterval).ToString("0.00");
        }
    }
}
