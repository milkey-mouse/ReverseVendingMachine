using ClientApp;
using ClientApp.Model;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace BorderLess
{
    /// <summary>
    /// Interaction logic for WindowBorderLess.xaml
    /// </summary>
    public partial class WindowBorderLess
    {
        private ClientApp.Model.FoodSource _foodSource;

        public WindowBorderLess()
        {
            InitializeComponent();

            _foodSource = new ClientApp.Model.FoodSource();
            _foodSource.Title = "Runtime Title";

            _foodSource.Foods = new ObservableCollection<ClientApp.Model.Food>()
                {
                };

            for (int i = 1; i <= 20; i++)
            {
                _foodSource.Foods.Add(new ClientApp.Model.Food()
                {
                    Category = "Category " + i.ToString() + "/",
                    Title = "Food " + i.ToString(),
                });
            }

            _foodSource.Foods.Add(new ClientApp.Model.Food()
            {
                Category = "First category/",
                Title = "First food",
            });
            

            DataContext = _foodSource;
        }

        private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Button btn = (Button)sender;
            string name = btn.Name;
            Console.WriteLine(name);
        }
    }
}
