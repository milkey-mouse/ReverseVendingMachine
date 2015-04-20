using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace ClientApp.Model
{
    class FoodSource
    {
        public string Title { get; set; }
        public ObservableCollection<Food> Foods { get; set; }
    }
}
