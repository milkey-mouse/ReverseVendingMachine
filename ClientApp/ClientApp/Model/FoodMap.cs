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
            Map(m => m.Title).Name("Name"); //couldn't call it "Name" because of conflicts
            Map(m => m.SubCatRoot).Name("Category"); //same with this
        }
    }
}
