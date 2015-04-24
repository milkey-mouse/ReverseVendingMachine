using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClientApp.Model
{
    public sealed class FoodMap : CsvClassMap<Food>
    {
        public FoodMap()
        {
            //Map(m => m.URL).Name("Title"); //couldn't call it "Name" because of conflicts
            //Map(m => m.Category).Name("SubCatRoot"); //same with this, other SubCat's are labeled correctly
        }
    }
}
