﻿using ClientApp;
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
            CacheBuilt = true;
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

        //Tuple<List<String>, List<String>> GetSearchWords()
        //{
        //    string[] all = SearchBox.Text.ToLower().Split("\"".ToCharArray());
        //    var isQuoted = false;
        //    List<String> quoteList = new List<String>();
        //    List<String> normalList = new List<String>();
        //    foreach(string quote in all)
        //    {
        //        isQuoted = !isQuoted;
        //        if(isQuoted == true)
        //        {
        //            quoteList.Add(quote.Replace("  ", " "));
        //        }
        //        else
        //        {
        //            normalList.AddRange(quote.Replace("  ", " ").Split());
        //        }
        //    }
        //    if(isQuoted == true)
        //    {
        //        quoteList.
        //    }
        //    return Tuple.Create(quoteList, normalList);
        //}

        public void Reindex()
        {
            if(CacheBuilt == false)
            {
                return; //hasn't yet initialized foods
            }
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
                String[] searchWords = SearchBox.Text.ToLower().Split();
                List<String> orWords = new List<String>();
                List<String> andWords = new List<String>();
                List<String> notWords = new List<String>();
                bool andDefault = (searchWords[0].StartsWith("+"));
                if (andDefault)
                {
                    searchWords[0] = searchWords[0].Substring(1);
                }
                foreach (string searchWord in searchWords)
                {
                    if (searchWord == "")
                    {
                        return;
                    }
                    if (searchWord.StartsWith("-"))
                    {
                        notWords.Add(StripPunctuation(searchWord.Substring(1)));
                    }
                    else if (andDefault)
                    {
                        andWords.Add(StripPunctuation(searchWord));
                    }
                    else
                    {
                        orWords.Add(StripPunctuation(searchWord));
                    }
                }
                bool firstWord = true;
                if (orWords.Count > 0)
                {
                    foreach (KeyValuePair<String, List<ClientApp.Model.Food>> foodEntry in FoodCache)
                    {
                        firstWord = true;
                        foreach (string searchWord in orWords)
                        {
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
                }
                else if (andWords.Count > 0)
                {
                    Dictionary<ClientApp.Model.Food, List<String>> _andDict = new Dictionary<ClientApp.Model.Food, List<String>>();
                    foreach (KeyValuePair<String, List<ClientApp.Model.Food>> foodEntry in FoodCache)
                    {
                        foreach (string searchWord in andWords)
                        {
                            if (foodEntry.Key.StartsWith(searchWord))
                            {
                                foreach (ClientApp.Model.Food food in foodEntry.Value)
                                {
                                    if (_andDict.ContainsKey(food))
                                    {
                                        if(!_andDict[food].Contains(searchWord))
                                        {
                                            _andDict[food].Add(searchWord);
                                        }
                                    }
                                    else
                                    {
                                        _andDict.Add(food, new List<String>());
                                        _andDict[food].Add(searchWord);
                                    }
                                    if (_andDict[food].Count == andWords.Count && _resultsDict.ContainsKey(food) == false)
                                    {
                                        _resultsDict.Add(food, 0);
                                    }
                                }
                            }
                        }
                    }
                }
                if(notWords.Count > 0)
                {
                    foreach (KeyValuePair<String, List<ClientApp.Model.Food>> foodEntry in FoodCache)
                    {
                        foreach (string searchWord in notWords)
                        {
                            if (foodEntry.Key.StartsWith(searchWord))
                            {
                                foreach (ClientApp.Model.Food food in foodEntry.Value)
                                {
                                    if (_resultsDict.ContainsKey(food))
                                    {
                                        _resultsDict.Remove(food);
                                    }
                                }
                            }
                        }
                    }
                }
                if(andWords.Count == 0)
                {
                    var foods = from food in _resultsDict
                                orderby food.Value descending
                                select food.Key;
                    foreach (ClientApp.Model.Food food in foods)
                    {
                        _foodSource.Foods.Add(food);
                    }
                }
                else
                {
                    _foodSource.Foods = new ObservableCollection<Food>(_resultsDict.Keys);
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

        private void PropertyChecked(object sender, RoutedEventArgs e)
        {
            Reindex();
            UpdateSearch();
        }

        //private void Button_Click(object sender, RoutedEventArgs e)
        //{
        //    if (SearchBox.Text == "")
        //    {
        //        SearchLabel.Visibility = Visibility.Visible;

        //        DataContext = FoodData;
        //        FoodsView.ItemsSource = FoodData.Foods;
        //    }
        //    else
        //    {
        //        SearchLabel.Visibility = Visibility.Hidden;

        //        _foodSource = new ClientApp.Model.FoodSource();
        //        _foodSource.Title = FoodData.Title;
        //        _foodSource.Foods = new ObservableCollection<ClientApp.Model.Food>();
        //        Dictionary<ClientApp.Model.Food, int> _resultsDict = new Dictionary<ClientApp.Model.Food, int>();
        //        String[] searchWords = SearchBox.Text.ToLower().Split();
        //        List<String> orWords = new List<String>();
        //        List<String> andWords = new List<String>();
        //        List<String> notWords = new List<String>();
        //        bool andDefault = (searchWords[0].StartsWith("+"));
        //        if (andDefault)
        //        {
        //            searchWords[0] = searchWords[0].Substring(1);
        //        }
        //        foreach (string searchWord in searchWords)
        //        {
        //            if (searchWord == "")
        //            {
        //                return;
        //            }
        //            if (searchWord.StartsWith("-"))
        //            {
        //                notWords.Add(StripPunctuation(searchWord.Substring(1)));
        //            }
        //            else if (andDefault)
        //            {
        //                andWords.Add(StripPunctuation(searchWord));
        //            }
        //            else
        //            {
        //                orWords.Add(StripPunctuation(searchWord));
        //            }
        //        }
        //        bool firstWord = true;
        //        if (orWords.Count > 0)
        //        {
        //            foreach (KeyValuePair<String, List<ClientApp.Model.Food>> foodEntry in FoodCache)
        //            {
        //                firstWord = true;
        //                foreach (string searchWord in orWords)
        //                {
        //                    if (foodEntry.Key.StartsWith(searchWord))
        //                    {
        //                        foreach (ClientApp.Model.Food food in foodEntry.Value)
        //                        {
        //                            if (_resultsDict.ContainsKey(food))
        //                            {
        //                                _resultsDict[food] += 1;
        //                            }
        //                            else if (firstWord)
        //                            {
        //                                _resultsDict.Add(food, 0);
        //                            }
        //                        }
        //                    }
        //                    firstWord = false;
        //                }
        //            }
        //        }
        //        else if(andWords.Count() > 0)
        //        {
        //            Dictionary<String, List<ClientApp.Model.Food>> _andKeys = new Dictionary<String, List<ClientApp.Model.Food>>();
        //            foreach (var word in andWords)
        //            {
        //                _andKeys.Add(word, new List<ClientApp.Model.Food>());
        //            }
        //            foreach (KeyValuePair<String, List<ClientApp.Model.Food>> foodEntry in FoodCache)
        //            {
        //                foreach (var word in andWords)
        //                {
        //                    if(foodEntry.Key.StartsWith(word))
        //                    {
        //                        foreach (ClientApp.Model.Food food in foodEntry.Value)
        //                        {
        //                            _andKeys[word].Add(food);
        //                        }
        //                    }
        //                }
        //            }
        //            List<ClientApp.Model.Food> _andList = new List<ClientApp.Model.Food>(FoodData.Foods);
        //            foreach (var food in _andList)
        //            {
        //                foreach (string searchWord in andWords)
        //                {
        //                    if(_andKeys[searchWord].Contains(food) == false)
        //                    {
                                
        //                    }
        //                }
        //            }
        //        }
        //        var targetValue = andWords.Count();
        //        foreach(string word in andWords)
        //        {
        //            Console.Write(word + ", ");
        //        }
        //        foreach (KeyValuePair<ClientApp.Model.Food, int> food in _andDict)
        //        {
        //            Console.WriteLine(food.Key.Name + ":" + food.Value.ToString());
        //        }
        //    }
        //}
    }
}
