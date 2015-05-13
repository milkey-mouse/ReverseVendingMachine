using ClientApp;
using ClientApp.Model;
using CsvHelper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

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

        private Task LoadTask = null;

        private TaskScheduler Scheduler = null;

        bool BarcodeEmbiggened = false;

        Thickness BarcodeMargin = new Thickness();

        public WindowBorderLess()
        {
            InitializeComponent();

            Scheduler = TaskScheduler.FromCurrentSynchronizationContext();

            LoadingSplash.Visibility = Visibility.Visible;

            LoadTask = Task.Factory.StartNew<ClientApp.Model.FoodSource>(() =>
                {
                    FoodData = new ClientApp.Model.FoodSource();
                    FoodData.Title = "Reverse Vending Machine - Derpton Middle School";

                    Directory.CreateDirectory(AppDataPath); //it already checks for it

                    try
                    {
                        if(System.IO.File.Exists(AppDataPath + "new_products.csv"))
                        {
                            System.IO.File.Delete(AppDataPath + "products.csv");
                            System.IO.File.Copy(AppDataPath + "new_products.csv", AppDataPath + "products.csv");
                            System.IO.File.Delete(AppDataPath + "new_products.csv");
                        }
                    }
                    catch
                    {
                        try
                        {
                            System.IO.File.Delete(AppDataPath + "new_products.csv");
                        }
                        catch
                        {
                        }
                    }
                    
                    FoodData.Foods = new ObservableCollection<ClientApp.Model.Food>();

                    List<ClientApp.Model.Food> tempFoods = new List<ClientApp.Model.Food>();
                    foreach (var food in DownloadAndParse())
                    {
                        string[] searchprops = { food.Category, food.SubCat1, food.SubCat2, food.Name, food.UPC };
                        foreach (var word in string.Join(" ", searchprops).Split())
                        {
                            string target = StripPunctuation(word.ToLower());
                            if (FoodCache.ContainsKey(target) == false)
                            {
                                FoodCache.Add(target, new List<ClientApp.Model.Food>());
                            }
                            FoodCache[target].Add(food);
                        }
                        string[] foodpath = { food.Category, food.SubCat1, food.SubCat2, "" }; //the empty string is for the final slash
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
                    return FoodData;
                }).ContinueWith((i) => { FoodData = i.Result; DataContext = i.Result; FoodsView.ItemsSource = i.Result.Foods; LoadingSplash.Visibility = Visibility.Hidden; CacheBuilt = true; }, Scheduler);
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
            if (File.Exists(AppDataPath + "products.csv") == false)
            {
                DownloadAll(true, false);
            }
            else if (DateTime.Today - File.GetLastWriteTime(AppDataPath + "products.csv") > new TimeSpan(30, 0, 0, 0))
            {
                DownloadAll(false, false);
            }
            var csv = new CsvReader(File.OpenText(AppDataPath + "products.csv"));
            var records = csv.GetRecords<Food>(); //map the csv to the foods
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
                else if(notWords.Count > 0)
                {
                    foreach(ClientApp.Model.Food food in FoodData.Foods)
                    {
                        _resultsDict.Add(food, 0);
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
                //LoadingSplash.Visibility = Visibility.Visible;
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
            catch (Exception e)
            {
                if (File.Exists(AppDataPath + "products.csv") == false || showDialog)
                {
                    MessageBox.Show(e.ToString(), "Download Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    if (File.Exists(AppDataPath + "products.csv") == false)
                    {
                        Application.Current.Shutdown();
                    }
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

        private void FoodsView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FoodsView.SelectedItems.Count == 1)
            {
                ItemCount.Content = FoodsView.SelectedItems.Count + " item selected";
                ClientApp.Model.Food foodItem = (ClientApp.Model.Food)FoodsView.SelectedItem;
                ItemName.Text = foodItem.Name;
                BarcodeImage.Content = foodItem.UPC;
            }
            else
            {
                ItemCount.Content = FoodsView.SelectedItems.Count + " items selected";
                BarcodeImage.Content = "";
                if(FoodsView.SelectedItems.Count == 0)
                {
                    ItemName.Text = "No item selected";
                }
                else
                {
                    ItemName.Text = "Multiple items selected";
                }
            }
        }

        private void DBUpdate_Click(object sender, RoutedEventArgs e)
        {
            LoadingSplash.Visibility = Visibility.Visible;
            LoadTask = Task.Factory.StartNew<ClientApp.Model.FoodSource>(() =>
            {
                FoodData.Foods = new ObservableCollection<ClientApp.Model.Food>();
                List<ClientApp.Model.Food> tempFoods = new List<ClientApp.Model.Food>();
                var wc = new WebClient();
                String fileString = wc.DownloadString("http://team-ivan.com/rvm/all_products.csv");
                if(System.IO.File.Exists(AppDataPath + "new_products.csv"))
                {
                    System.IO.File.Delete(AppDataPath + "new_products.csv");
                }
                System.IO.File.WriteAllText(AppDataPath + "new_products.csv", fileString);
                StringReader stream = new StringReader(fileString);
                var csv = new CsvReader(stream);
                var records = csv.GetRecords<Food>(); //map the csv to the foods
                foreach (var food in records)
                {
                    string[] searchprops = { food.Category, food.SubCat1, food.SubCat2, food.Name, food.UPC };
                    foreach (var word in string.Join(" ", searchprops).Split())
                    {
                        string target = StripPunctuation(word.ToLower());
                        if (FoodCache.ContainsKey(target) == false)
                        {
                            FoodCache.Add(target, new List<ClientApp.Model.Food>());
                        }
                        FoodCache[target].Add(food);
                    }
                    string[] foodpath = { food.Category, food.SubCat1, food.SubCat2, "" }; //the empty string is for the final slash
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
                return FoodData;
            }).ContinueWith((i) =>
            {
                FoodData = i.Result; DataContext = i.Result; FoodsView.ItemsSource = i.Result.Foods; LoadingSplash.Visibility = Visibility.Hidden; CacheBuilt = true; Process.Start(Application.ResourceAssembly.Location); Application.Current.Shutdown(); }, Scheduler);
        }

        //shut up this is a great idea
        static public void DelayCall(int msec, Action fn)
        {
            // Grab the dispatcher from the current executing thread
            Dispatcher d = Dispatcher.CurrentDispatcher;

            // Tasks execute in a thread pool thread
            new Task(() =>
            {
                System.Threading.Thread.Sleep(msec);   // delay

                // use the dispatcher to asynchronously invoke the action 
                // back on the original thread
                d.BeginInvoke(fn);
            }).Start();
        }

        private void EmbiggenBarcode_Up(object sender, EventArgs e)
        {
            EmbiggenBarcode.IsEnabled = false;
            if(BarcodeEmbiggened)
            {
                EmbiggenBarcode.Source = new BitmapImage(new Uri("Expand-76.png", UriKind.Relative));
                PowerEase pe = new PowerEase();
                pe.Power = 2.5;
                Size bigSize = new Size(Barcode.Width / 2.25, Barcode.Height / 3);
                Duration animationDuration = new Duration(TimeSpan.FromSeconds(0.5));
                DoubleAnimation dw = new DoubleAnimation();
                dw.EasingFunction = pe;
                dw.From = Barcode.Width;
                dw.To = bigSize.Width;
                dw.Duration = animationDuration;
                DoubleAnimation dh = new DoubleAnimation();
                dh.EasingFunction = pe;
                dh.From = Barcode.Height;
                dh.To = bigSize.Height;
                dh.Duration = animationDuration;
                ThicknessAnimation dm = new ThicknessAnimation();
                dm.EasingFunction = pe;
                dm.From = Barcode.Margin;
                dm.To = BarcodeMargin;
                dm.Duration = animationDuration;
                ColorAnimation pc = new ColorAnimation();
                pc.EasingFunction = pe;
                Color blackFrom = Colors.Black;
                blackFrom.A = 153;
                pc.From = blackFrom;
                blackFrom.A = 0;
                pc.To = blackFrom;
                pc.Duration = animationDuration;
                DelayCall(500, new Action(() => { BarcodeSplash.Visibility = Visibility.Hidden; }));
                BarcodeSplash.Background.BeginAnimation(SolidColorBrush.ColorProperty, pc);
                Barcode.BeginAnimation(Grid.HeightProperty, dh);
                Barcode.BeginAnimation(Grid.WidthProperty, dw);
                Barcode.BeginAnimation(Grid.MarginProperty, dm);
            }
            else
            {
                EmbiggenBarcode.Source = new BitmapImage(new Uri("Retract-76.png", UriKind.Relative));
                BarcodeMargin = Barcode.Margin;
                PowerEase pe = new PowerEase();
                pe.Power = 2.5;
                Size bigSize = new Size(Barcode.Width * 2.25, Barcode.Height * 3);
                Duration animationDuration = new Duration(TimeSpan.FromSeconds(0.5));
                DoubleAnimation dw = new DoubleAnimation();
                dw.EasingFunction = pe;
                dw.From = Barcode.Width;
                dw.To = bigSize.Width;
                dw.Duration = animationDuration;
                DoubleAnimation dh = new DoubleAnimation();
                dh.EasingFunction = pe;
                dh.From = Barcode.Height;
                dh.To = bigSize.Height;
                dh.Duration = animationDuration;
                ThicknessAnimation dm = new ThicknessAnimation();
                dm.EasingFunction = pe;
                dm.From = Barcode.Margin;
                dm.To = new Thickness((this.Width - bigSize.Width) / 2, Barcode.Margin.Top, Barcode.Margin.Right, (this.Height - bigSize.Height) / 2);
                dm.Duration = animationDuration;
                ColorAnimation pc = new ColorAnimation();
                pc.EasingFunction = pe;
                Color blackFrom = Colors.Black;
                blackFrom.A = 0;
                pc.From = blackFrom;
                blackFrom.A = 153;
                pc.To = blackFrom;
                pc.Duration = animationDuration;
                BarcodeSplash.Visibility = Visibility.Visible;
                BarcodeSplash.Background.BeginAnimation(SolidColorBrush.ColorProperty, pc);
                Barcode.BeginAnimation(Grid.HeightProperty, dh);
                Barcode.BeginAnimation(Grid.WidthProperty, dw);
                Barcode.BeginAnimation(Grid.MarginProperty, dm);
            }
            DelayCall(510, new Action(() => { EmbiggenBarcode.IsEnabled = true; }));
            BarcodeEmbiggened = !BarcodeEmbiggened;
        }
    }
}
