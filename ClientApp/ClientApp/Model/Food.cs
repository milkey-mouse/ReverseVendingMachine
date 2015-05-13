using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClientApp.Model
{
    public class Food
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public string SubCat1 { get; set; }
        public string SubCat2 { get; set; }
        public string Manufacturer { get; set; }
        public string URL { get; set; }
        public string UPC { get; set; }
        public string[] IngredientsArray;
        public string Ingredients { set { IngredientsArray = value.Split(";".ToCharArray());} }
        public bool Allowed = true;
    }
}
