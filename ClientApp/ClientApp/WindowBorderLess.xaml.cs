using System;
using System.Windows.Controls;
namespace BorderLess
{
    /// <summary>
    /// Interaction logic for WindowBorderLess.xaml
    /// </summary>
    public partial class WindowBorderLess
    {
        public WindowBorderLess()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Button btn = (Button)sender;
            string name = btn.Name;
            Console.WriteLine(name);
        }
    }
}
