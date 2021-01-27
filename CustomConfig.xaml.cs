using System;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Driver.AorusIT8297
{
    /// <summary>
    /// Interaction logic for CustomConfig.xaml
    /// </summary>
    public partial class CustomConfig : UserControl
    {
        public CustomConfig()
        {
            InitializeComponent();
        }

        public Action<int, int> SetLEDCounts;

        private void UpdateLEDCounts(object sender, TextChangedEventArgs e)
        {
            try
            {
                SetLEDCounts?.Invoke(int.Parse(argb1.Text), int.Parse(argb2.Text));
            }
            catch
            {
            }
        }
    }
}
