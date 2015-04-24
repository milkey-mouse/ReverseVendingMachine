using ClientApp;
using ClientApp.Model;
using CsvHelper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net;
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

        public string AppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Reverse Vending Machine\\";

        public bool DisableSplashOnDownload = true;

        public WindowBorderLess()
        {
            InitializeComponent();

            _foodSource = new ClientApp.Model.FoodSource();
            _foodSource.Title = "Reverse Vending Machine - Derpton Middle School";

            Directory.CreateDirectory(AppDataPath); //it already checks for it

            _foodSource.Foods = new ObservableCollection<ClientApp.Model.Food>()
                {
                    //DownloadAndParse().ToArray()
                };

            foreach (var food in DownloadAndParse())
            {
                if (food.Name.StartsWith(food.Manufacturer))
                {
                    food.Name = food.Name.Substring(food.Manufacturer.Length + 1); //quickest manipulation to remove manufacturer
                }
                //food.Category = food.Category + "/" + food.SubCat1 + "/" + food.SubCat2 + "/";
                string[] foodpath = {food.Category, food.SubCat1, food.SubCat2, ""}; //the empty string is for the final slash
                food.Category = string.Join("/", foodpath);
                _foodSource.Foods.Add(food);
            }

            //for (int i = 1; i <= 2000; i++)
            //{
            //    _foodSource.Foods.Add(new ClientApp.Model.Food()
            //    {
            //        Category = "Category " + i.ToString() + "/",
            //        Title = "Food " + i.ToString(),
            //    });
            //}
            //
            //_foodSource.Foods.Add(new ClientApp.Model.Food()
            //{
            //    Category = "First category/",
            //    Title = "First food",
            //});
            

            DataContext = _foodSource; //sets away from designer data source, only runs on compile
        }

        private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Button btn = (Button)sender;
            string name = btn.Name;
            Console.WriteLine(name);
        }

        private IEnumerable<Food> DownloadAndParse()
        {
            DisableSplashOnDownload = false;
            //DownloadAll(false, false);
            new WebClient().DownloadFile(new Uri("http://team-ivan.com/rvm/all_products.csv"), AppDataPath + "products.csv");
            var csv = new CsvReader(File.OpenText(AppDataPath + "products.csv"));
            var records = csv.GetRecords<Food>(); //map the csv to the foods
            LoadingSplash.Visibility = Visibility.Hidden;
            DisableSplashOnDownload = true;
            return records;
        }

        private void DownloadAll(bool showDialog, bool async = true)
        {
            try
            {
                LoadingSplash.Visibility = Visibility.Visible;
                WebClient webClient = new WebClient();
                webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadCompleted);
                if (async)
                {
                    webClient.DownloadFileAsync(new Uri("http://team-ivan.com/rvm/all_products.csv"), AppDataPath + "products.csv");
                }
                else
                {
                    webClient.DownloadFileAsync(new Uri("http://team-ivan.com/rvm/all_products.csv"), AppDataPath + "products.csv");
                }
            }
            catch (Exception)
            {
                if(File.Exists(AppDataPath + "products.csv") == false || showDialog)
                {
                    MessageBox.Show("Could not download product metadata. Please check your Internet connection.", "Download Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Application.Current.Shutdown();
                }
            }
        }

        private void DownloadCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (DisableSplashOnDownload)
            {
                LoadingSplash.Visibility = Visibility.Hidden;
            }
        }
    }
}
