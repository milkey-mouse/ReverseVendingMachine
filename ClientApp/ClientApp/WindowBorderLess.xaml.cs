﻿using ClientApp;
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
using System.Xml;

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

        ObservableCollection<Food> NoIngredientsFoodData = new ObservableCollection<Food>();

        ObservableCollection<Food> WithIngredientsFoodData = new ObservableCollection<Food>();

        public Dictionary<String, List<ClientApp.Model.Food>> FoodCache = new Dictionary<String, List<ClientApp.Model.Food>>();

        public bool CacheBuilt = false;

        public bool RemoveNoIngredients = false;

        private Task LoadTask = null;

        private TaskScheduler Scheduler = null;

        bool BarcodeEmbiggened = false;

        Thickness BarcodeMargin = new Thickness();

        Duration AnimationDuration = new Duration(TimeSpan.FromSeconds(0.5));


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
                }).ContinueWith((i) => {
                    FoodData = i.Result;
                    DataContext = i.Result;
                    FoodsView.ItemsSource = i.Result.Foods;
                    WithIngredientsFoodData = i.Result.Foods;
                    Task.Factory.StartNew<ObservableCollection<Food>>((x) =>
                    {
                        return new ObservableCollection<Food>(FoodData.Foods.Where(food => food.Ingredients != ""));
                    }, Scheduler).ContinueWith((y) =>
                    {
                        NoIngredientsFoodData = y.Result;
                        LoadingSplash.Visibility = Visibility.Hidden;
                        CacheBuilt = true;
                    });
                }, Scheduler);
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
                if (IngredientsBox.IsChecked == true)
                {
                    searchprops.Add(string.Join(" ", food.IngredientsArray));
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
            csv.Configuration.WillThrowOnMissingField = false;
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
                ItemCategory.Text = foodItem.Category;
                if(foodItem.Allowed)
                {
                    ItemAllowed.Text = "Yes";
                }
                else
                {
                    ItemAllowed.Text = "No";
                }
                ItemManufacturer.Text = foodItem.Manufacturer;
                BarcodeImage.Content = foodItem.UPC;
                IngredientsView.ItemsSource = foodItem.IngredientsArray;
                if(foodItem.Ingredients != "")
                {
                    NoIngredientsGrid.Visibility = Visibility.Hidden;
                }
                else
                {
                    NoIngredientsGrid.Visibility = Visibility.Visible;
                }
            }
            else
            {
                ItemCount.Content = FoodsView.SelectedItems.Count + " items selected";
                BarcodeImage.Content = "";
                ItemCategory.Text = "";
                ItemAllowed.Text = "";
                ItemManufacturer.Text = "";
                NoIngredientsGrid.Visibility = Visibility.Hidden;
                IngredientsView.ItemsSource = null;
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
            LoadTask = Task.Factory.StartNew(() =>
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
            }).ContinueWith((i) =>
            {
                Process.Start(Application.ResourceAssembly.Location); Application.Current.Shutdown(); }, Scheduler);
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
            if (EmbiggenBarcode.IsEnabled == false)
            {
                return;
            }
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
                DoubleAnimation dw = new DoubleAnimation();
                dw.EasingFunction = pe;
                dw.From = Barcode.Width;
                dw.To = bigSize.Width;
                dw.Duration = AnimationDuration;
                DoubleAnimation dh = new DoubleAnimation();
                dh.EasingFunction = pe;
                dh.From = Barcode.Height;
                dh.To = bigSize.Height;
                dh.Duration = AnimationDuration;
                ThicknessAnimation dm = new ThicknessAnimation();
                dm.EasingFunction = pe;
                dm.From = Barcode.Margin;
                dm.To = new Thickness((this.Width - bigSize.Width) / 2, Barcode.Margin.Top, Barcode.Margin.Right, (this.Height - bigSize.Height) / 2);
                dm.Duration = AnimationDuration;
                ColorAnimation pc = new ColorAnimation();
                pc.EasingFunction = pe;
                Color blackFrom = Colors.Black;
                blackFrom.A = 0;
                pc.From = blackFrom;
                blackFrom.A = 153;
                pc.To = blackFrom;
                pc.Duration = AnimationDuration;
                BarcodeSplash.Visibility = Visibility.Visible;
                BarcodeSplash.Background.BeginAnimation(SolidColorBrush.ColorProperty, pc);
                Barcode.BeginAnimation(Grid.HeightProperty, dh);
                Barcode.BeginAnimation(Grid.WidthProperty, dw);
                Barcode.BeginAnimation(Grid.MarginProperty, dm);
            }
            DelayCall(510, new Action(() => { EmbiggenBarcode.IsEnabled = true; }));
            BarcodeEmbiggened = !BarcodeEmbiggened;
        }

        private void InfoButton_Click(object sender, RoutedEventArgs e)
        {
            InfoButton.IsEnabled = false;
            HelpDialogClose.IsEnabled = true;
            PowerEase pe = new PowerEase();
            pe.Power = 2.5;
            ThicknessAnimation dm = new ThicknessAnimation();
            dm.EasingFunction = pe;
            dm.From = HelpDialog.Margin;
            dm.To = new Thickness((this.Width - HelpDialog.Width) / 2, ((this.Height - HelpDialog.Height) / 2) - 20, Barcode.Margin.Right, Barcode.Margin.Bottom);
            dm.Duration = AnimationDuration;
            ColorAnimation pc = new ColorAnimation();
            pc.EasingFunction = pe;
            Color blackFrom = Colors.Black;
            blackFrom.A = 0;
            pc.From = blackFrom;
            blackFrom.A = 153;
            pc.To = blackFrom;
            pc.Duration = AnimationDuration;
            HelpSplash.Visibility = Visibility.Visible;
            HelpDialog.BeginAnimation(Grid.MarginProperty, dm);
            HelpSplash.Background.BeginAnimation(SolidColorBrush.ColorProperty, pc);
        }

        private void HelpDialogClose_Click(object sender, RoutedEventArgs e)
        {
            if (HelpDialogClose.IsEnabled == false)
            {
                return;
            }
            HelpDialogClose.IsEnabled = false;
            PowerEase pe = new PowerEase();
            pe.Power = 2.5;
            ThicknessAnimation dm = new ThicknessAnimation();
            dm.EasingFunction = pe;
            dm.From = HelpDialog.Margin;
            dm.To = new Thickness((this.Width - HelpDialog.Width) / 2, -500, Barcode.Margin.Right, Barcode.Margin.Bottom);
            dm.Duration = AnimationDuration;
            ColorAnimation pc = new ColorAnimation();
            pc.EasingFunction = pe;
            Color blackFrom = Colors.Black;
            blackFrom.A = 153;
            pc.From = blackFrom;
            blackFrom.A = 0;
            pc.To = blackFrom;
            pc.Duration = AnimationDuration;
            HelpSplash.Visibility = Visibility.Visible;
            HelpDialog.BeginAnimation(Grid.MarginProperty, dm);
            HelpSplash.Background.BeginAnimation(SolidColorBrush.ColorProperty, pc);
            DelayCall(500, new Action(() => { InfoButton.IsEnabled = true; HelpSplash.Visibility = Visibility.Hidden; }));
        }

        private void ToggleHideIngredients(object sender, EventArgs e)
        {
            RemoveNoIngredients = !RemoveNoIngredients;
            if (RemoveNoIngredients == true)
            {
                FoodsCheckPic.Source = new BitmapImage(new Uri("Ingredients-27.png", UriKind.Relative));
                FoodData.Foods = NoIngredientsFoodData;
                FoodsView.ItemsSource = FoodData.Foods;
            }
            else
            {
                FoodsCheckPic.Source = new BitmapImage(new Uri("No_Ingredients-27.png", UriKind.Relative));
                FoodData.Foods = WithIngredientsFoodData;
                FoodsView.ItemsSource = FoodData.Foods;
            }
            Reindex();
            UpdateSearch();
        }

        public static T FindDescendant<T>(DependencyObject obj) where T : DependencyObject
        {
            if (obj == null) return default(T);
            int numberChildren = VisualTreeHelper.GetChildrenCount(obj);
            if (numberChildren == 0) return default(T);

            for (int i = 0; i < numberChildren; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child is T)
                {
                    return (T)(object)child;
                }
            }

            for (int i = 0; i < numberChildren; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                var potentialMatch = FindDescendant<T>(child);
                if (potentialMatch != default(T))
                {
                    return potentialMatch;
                }
            }

            return default(T);
        }

        private void ToggleAllowed(object sender, RoutedEventArgs e)
        {
            Button sb = sender as Button;
            Food dc = sb.DataContext as Food;
            Grid sp = null;
            Image gi = null;
            Image ri = null;
            RotateTransform rt = null;
            RotateTransform srt = null;
            bool HasUIChecked = false;
            bool StopChecking = false;
            if (FoodsView.SelectedItems.Contains(dc) && FoodsView.SelectedItems.Count > 1)
            {
                ScrollViewer s = FindDescendant<ScrollViewer>(FoodsView);
                double lv = Math.Floor(s.VerticalOffset);
                sp = sb.Parent as Grid;
                srt = sp.RenderTransform as RotateTransform;
                bool srta = false;
                if (srt.Angle == -45)
                {
                    srta = true;
                }
                foreach (Food item in FoodsView.SelectedItems)
                {
                    //WPF is so intuitive, they said...
                    //Databinding is easy, they said...
                    ListBoxItem listItem = null;
                    if (StopChecking == false && FoodsView.Items.IndexOf(item) >= lv)
                    {
                        listItem = this.FoodsView.ItemContainerGenerator.ContainerFromIndex(FoodsView.Items.IndexOf(item)) as ListBoxItem;
                    }
                    if (listItem == null) //the item is offscreen
                    {
                        if(HasUIChecked == true && StopChecking == false)
                        {
                            StopChecking = true;
                            //Console.WriteLine("End:" + item.Name);
                        }
                        item.Allowed = srta;
                        continue;
                    }
                    else
                    {
                        HasUIChecked = true;
                    }
                    DataTemplate dtx = listItem.ContentTemplate;
                    Border bx = VisualTreeHelper.GetChild(listItem, 0) as Border;
                    ContentPresenter cpx = bx.Child as ContentPresenter;
                    StackPanel spx = dtx.FindName("spOuterPanel", cpx) as StackPanel;
                    Grid grx = spx.FindName("CheckGrid") as Grid;
                    sp = grx;
                    gi = sp.Children[0] as Image;
                    ri = sp.Children[1] as Image;
                    rt = sp.RenderTransform as RotateTransform;
                    dc = sp.DataContext as Food;
                    if (srt.Angle == 0)
                    {
                        dc.Allowed = false;
                        PowerEase pe = new PowerEase();
                        pe.Power = 2.5;
                        DoubleAnimation gio = new DoubleAnimation();
                        gio.EasingFunction = pe;
                        gio.From = gi.Opacity;
                        gio.To = 0;
                        gio.Duration = AnimationDuration;
                        DoubleAnimation rio = new DoubleAnimation();
                        rio.EasingFunction = pe;
                        rio.From = ri.Opacity;
                        rio.To = 1;
                        rio.Duration = AnimationDuration;
                        DoubleAnimation rta = new DoubleAnimation();
                        rta.EasingFunction = pe;
                        rta.From = rt.Angle;
                        rta.To = -45;
                        rta.Duration = AnimationDuration;
                        gi.BeginAnimation(OpacityProperty, gio);
                        ri.BeginAnimation(OpacityProperty, rio);
                        rt.BeginAnimation(RotateTransform.AngleProperty, rta);
                    }
                    else if (srt.Angle == -45)
                    {
                        dc.Allowed = true;
                        PowerEase pe = new PowerEase();
                        pe.Power = 2.5;
                        DoubleAnimation gio = new DoubleAnimation();
                        gio.EasingFunction = pe;
                        gio.From = gi.Opacity;
                        gio.To = 1;
                        gio.Duration = AnimationDuration;
                        DoubleAnimation rio = new DoubleAnimation();
                        rio.EasingFunction = pe;
                        rio.From = ri.Opacity;
                        rio.To = 0;
                        rio.Duration = AnimationDuration;
                        DoubleAnimation rta = new DoubleAnimation();
                        rta.EasingFunction = pe;
                        rta.From = rt.Angle;
                        rta.To = 0;
                        rta.Duration = AnimationDuration;
                        gi.BeginAnimation(OpacityProperty, gio);
                        ri.BeginAnimation(OpacityProperty, rio);
                        rt.BeginAnimation(RotateTransform.AngleProperty, rta);
                    }
                }
                DelayCall(500, FoodsView.Items.Refresh);
            }
            else
            {
                sp = sb.Parent as Grid;
                gi = sp.Children[0] as Image;
                ri = sp.Children[1] as Image;
                rt = sp.RenderTransform as RotateTransform;
                dc = sb.DataContext as Food;
                if (rt.Angle == 0)
                {
                    dc.Allowed = false;
                    if (FoodsView.SelectedItems.Contains(dc))
                    {
                        ItemAllowed.Text = "No";
                    }
                    PowerEase pe = new PowerEase();
                    pe.Power = 2.5;
                    DoubleAnimation gio = new DoubleAnimation();
                    gio.EasingFunction = pe;
                    gio.From = gi.Opacity;
                    gio.To = 0;
                    gio.Duration = AnimationDuration;
                    DoubleAnimation rio = new DoubleAnimation();
                    rio.EasingFunction = pe;
                    rio.From = ri.Opacity;
                    rio.To = 1;
                    rio.Duration = AnimationDuration;
                    DoubleAnimation rta = new DoubleAnimation();
                    rta.EasingFunction = pe;
                    rta.From = rt.Angle;
                    rta.To = -45;
                    rta.Duration = AnimationDuration;
                    gi.BeginAnimation(OpacityProperty, gio);
                    ri.BeginAnimation(OpacityProperty, rio);
                    rt.BeginAnimation(RotateTransform.AngleProperty, rta);
                }
                else if (rt.Angle == -45)
                {
                    dc.Allowed = true;
                    if (FoodsView.SelectedItems.Contains(dc))
                    {
                        ItemAllowed.Text = "Yes";
                    }
                    PowerEase pe = new PowerEase();
                    pe.Power = 2.5;
                    DoubleAnimation gio = new DoubleAnimation();
                    gio.EasingFunction = pe;
                    gio.From = gi.Opacity;
                    gio.To = 1;
                    gio.Duration = AnimationDuration;
                    DoubleAnimation rio = new DoubleAnimation();
                    rio.EasingFunction = pe;
                    rio.From = ri.Opacity;
                    rio.To = 0;
                    rio.Duration = AnimationDuration;
                    DoubleAnimation rta = new DoubleAnimation();
                    rta.EasingFunction = pe;
                    rta.From = rt.Angle;
                    rta.To = 0;
                    rta.Duration = AnimationDuration;
                    gi.BeginAnimation(OpacityProperty, gio);
                    ri.BeginAnimation(OpacityProperty, rio);
                    rt.BeginAnimation(RotateTransform.AngleProperty, rta);
                }
            }
        }
    }
}
