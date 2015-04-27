using ClientApp;
using ClientApp.Model;
using CsvHelper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;
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

        ClientApp.Model.FoodSource FoodData = new ClientApp.Model.FoodSource();

        public Dictionary<String, List<ClientApp.Model.Food>> FoodCache = new Dictionary<String, List<ClientApp.Model.Food>>();

        public bool CacheBuilt = false;

        public WindowBorderLess()
        {
            InitializeComponent();

            FoodData = new ClientApp.Model.FoodSource();
            FoodData.Title = "Reverse Vending Machine - Derpton Middle School";

            Directory.CreateDirectory(AppDataPath); //it already checks for it

            FoodData.Foods = new ObservableCollection<ClientApp.Model.Food>();

            List<ClientApp.Model.Food> tempFoods = new List<ClientApp.Model.Food>();

            foreach (var food in DownloadAndParse())
            {
                string[] searchprops = {food.Category, food.SubCat1, food.SubCat2, food.Name, food.UPC};
                foreach (var word in string.Join(" ", searchprops).Split())
                {
                    string target = StripPunctuation(word.ToLower());
                    if (FoodCache.ContainsKey(target) == false)
                    {
                        FoodCache.Add(target, new List<ClientApp.Model.Food>());
                    }
                    FoodCache[target].Add(food);
                }
                string[] foodpath = {food.Category, food.SubCat1, food.SubCat2, ""}; //the empty string is for the final slash
                food.Category = string.Join("/", foodpath);
                tempFoods.Add(food);
            }
            var foods = from food in tempFoods
                        orderby food.Name ascending
                        select food;
            foreach (ClientApp.Model.Food food in foods)
            {
                FoodData.Foods.Add(food);
            }
            DataContext = FoodData; //sets away from designer data source, only runs on compile
            FoodsView.ItemsSource = FoodData.Foods;
        }

        string StripPunctuation(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                if (!char.IsPunctuation(c))
                    sb.Append(c);
            }
            return sb.ToString();
        }

        void Reindex()
        {
            FoodCache = new Dictionary<String, List<ClientApp.Model.Food>>();
            var splitslash = "/".ToCharArray();
            foreach (var food in FoodData.Foods)
            {
                List<string> searchprops = new List<string>(); //{food.Category, food.SubCat1, food.SubCat2, food.Manufacturer, food.Name, food.UPC };
                if(ManufacturerBox.IsChecked == true)
                {
                    searchprops.Add(food.Manufacturer);
                }
                if (CategoryBox.IsChecked == true)
                {
                    foreach(var prop in food.Category.Split(splitslash))
                    {
                        searchprops.Add(prop);
                    }
                }
                if (NameBox.IsChecked == true)
                {
                    searchprops.Add(food.Name);
                }
                if (UPCBox.IsChecked == true)
                {
                    searchprops.Add(food.UPC);
                }
                foreach (var word in string.Join(" ", searchprops).Split())
                {
                    string target = StripPunctuation(word.ToLower());
                    if (FoodCache.ContainsKey(target) == false)
                    {
                        FoodCache.Add(target, new List<ClientApp.Model.Food>());
                    }
                    FoodCache[target].Add(food);
                }
            }
        }

        private IEnumerable<Food> DownloadAndParse()
        {
            DisableSplashOnDownload = false;
            if(File.Exists(AppDataPath + "products.csv") == false)
            {
                DownloadAll(true, false);
            }
            var csv = new CsvReader(File.OpenText(AppDataPath + "products.csv"));
            var records = csv.GetRecords<Food>(); //map the csv to the foods
            LoadingSplash.Visibility = Visibility.Hidden;
            DisableSplashOnDownload = true;
            return records;
        }

        private void SearchUpdate(object sender, TextChangedEventArgs e)
        {
            UpdateSearch();
        }

        void UpdateSearch()
        {
            if (SearchBox.Text == "")
            {
                SearchLabel.Visibility = Visibility.Visible;

                DataContext = FoodData;
                FoodsView.ItemsSource = FoodData.Foods;
            }
            else
            {
                SearchLabel.Visibility = Visibility.Hidden;

                _foodSource = new ClientApp.Model.FoodSource();
                _foodSource.Title = FoodData.Title;
                _foodSource.Foods = new ObservableCollection<ClientApp.Model.Food>();
                Dictionary<ClientApp.Model.Food, int> _resultsDict = new Dictionary<ClientApp.Model.Food, int>();
                String[] searchWords = StripPunctuation(SearchBox.Text.ToLower()).Split();
                bool firstWord = true;
                foreach (KeyValuePair<String, List<ClientApp.Model.Food>> foodEntry in FoodCache)
                {
                    firstWord = true;
                    foreach (string searchWord in searchWords)
                    {
                        if(searchWord == "")
                        {
                            continue;
                        }
                        if (foodEntry.Key.StartsWith(searchWord))
                        {
                            foreach (ClientApp.Model.Food food in foodEntry.Value)
                            {
                                if (_resultsDict.ContainsKey(food))
                                {
                                    _resultsDict[food] += 1;
                                }
                                else if (firstWord)
                                {
                                    _resultsDict.Add(food, 0);
                                }
                            }
                        }
                        firstWord = false;
                    }
                }
                var foods = from food in _resultsDict
                            orderby food.Value descending
                            select food.Key;
                foreach (ClientApp.Model.Food food in foods)
                {
                    _foodSource.Foods.Add(food);
                }
                DataContext = _foodSource;
                FoodsView.ItemsSource = _foodSource.Foods;
            }
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
                    webClient.DownloadFile(new Uri("http://team-ivan.com/rvm/all_products.csv"), AppDataPath + "products.csv");
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

        private void SearchLabelMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SearchBox.Focus();
        }

        private void SearchLabel_StylusDown(object sender, System.Windows.Input.StylusDownEventArgs e)
        {
            SearchBox.Focus();
        }

        private void SearchLabel_TouchDown(object sender, System.Windows.Input.TouchEventArgs e)
        {
            SearchBox.Focus();
        }


        private void CategoryBox_Checked(object sender, RoutedEventArgs e)
        {
            //Reindex();
            UpdateSearch();
        }

        private void ManufacturerBox_Checked(object sender, RoutedEventArgs e)
        {
            //Reindex();
            UpdateSearch();
        }

        private void NameBox_Checked(object sender, RoutedEventArgs e)
        {
            //Reindex();
            UpdateSearch();
        }

        private void UPCBox_Checked(object sender, RoutedEventArgs e)
        {
            //Reindex();
            UpdateSearch();
        }
    }
}
