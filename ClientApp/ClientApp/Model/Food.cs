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
        string IngredientsString = "";
        public string Ingredients { set { IngredientsArray = value.Split(";".ToCharArray()); IngredientsString = value; } get { return IngredientsString; } }
        public bool Allowed = true;
    }
}
