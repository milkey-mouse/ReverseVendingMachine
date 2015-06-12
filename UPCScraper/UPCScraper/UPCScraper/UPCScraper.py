import simplejson as json
from lxml import etree
from lxml import html
import threading
import requests
import shutil
import lxml
import glob
import csv
import os

print "Getting target pages..."

categories = []

r = requests.get("http://www.upcfoodsearch.com/products/")
cattree = lxml.html.fromstring(r.text)

denied = ["baby_food", "baking_supplies", "cooking_supplies", "pet_food", "vitamins_and_supplements"]

for element in cattree.find_class("click1"):
    name = element.get("href")
    name = name[:-1]
    name = name[name.rfind("/") + 1:]
    name = name.replace("-", "_") #all_the_cool_kids_use_underscores
    if not name in denied:
        categories.append(element.get("href"))

print "Spawning threads..."

#krogerquery = """[
#  "query":"{0}",
#  "pageNumber":1,
#  "pageSizes":[
#    "products":1,
#    "recipes":1,
#    "contents":1,
#    "coupons":1
#  ],
#  "storeId":null,
#  "divisionId":null
#]"""

krogerquery = """{
  "query":"{{fn}}",
  "pageNumber":1,
  "pageSizes":{
    "products":20,
    "recipes":1,
    "contents":1,
    "coupons":1
  },
  "storeId":null,
  "divisionId":null
}"""

units = {
    'kg':          1.,
    'kilo':        1.,
    'kilogram':    1.,
    'lb':          2.204,
    'pound':       2.204,
    'ton':         0.001,
    'gram':        1000.,
    'g':           1000.,
    'ounce':       35.27,
    'oz':          35.27,
}

def getUnit(unit_name):
    if unit_name in units:
        return units[unit_name]
    # recognize regular plural forms
    elif unit_name.endswith('es') and unit_name[:-2] in units:
        return units[unit_name[:-2]]
    elif unit_name.endswith('s') and unit_name[:-1] in units:
        return units[unit_name[:-1]]
    # not found?
    else:
        raise ValueError("Unrecognized unit '{0}'".format(unit_name))

def convert(amt, from_unit, to_unit):
    numA = getUnit(from_unit)
    numB = getUnit(to_unit)
    return amt * numB / numA

def krogerweight(name,upc):
    try:
        rstring = krogerquery.replace("{{fn}}", name) #krogerquery.format(name).replace("[", "{").replace("]", "}")
        rk = requests.post("https://www.kroger.com/internalcontent/searchAll", data=rstring)
        rq = requests.post("https://www.qfc.com/internalcontent/searchAll", data=rstring, headers={"Content-Type" : "application/json"})
        rkt = []
        rqt = []
        try:
            rkt = json.loads(rk.text)["product"]["items"]
        except:
            rkt = []
        try:
            rqt = json.loads(rq.text)["product"]["items"]
        except:
            rqt = []
        ji = rkt + rqt
        #print ji
        for food in ji:
            if upc[:-1] in food["upc"] or name.lower() in food["description"].lower():
                #pprint.pprint(food)
                fws = "sos"
                if not food["itemSize"] == "":
                    fws = food["itemSize"].lower().split()
                elif not food["customerFacingSize"] == "":
                    fws = food["customerFacingSize"].lower().split()
                try:
                    return str(round(convert(float(fws[0]), fws[1], "g")))
                except:
                    pass
        return ""
    except:
        return ""
                    

def download_all(url):
    name = url
    name = name[:-1]
    name = name[name.rfind("/") + 1:]
    name = name.replace("-", "_") #all_the_cool_kids_use_underscores
    with open(name + ".csv", "w") as prodfile:
        csvfile = csv.writer(prodfile)
        csvfile.writerow(("Name","UPC","Category","SubCat1","SubCat2","Manufacturer","URL","Ingredients","Weight"))
        pagenum = 1
        while True:
            #try:
            r = requests.get(url)
            cattree = lxml.html.fromstring(r.text)
            el_cat1 = cattree.find_class("blockTitleL cen")[0][0].text_content() #just grab the header
            for element in (cattree.find_class("ewTableRow") + cattree.find_class("ewTableAltRow")):
                try:
                    el_name = element[1][0][0].text_content()
                except:
                    el_name = ""
                try:
                    el_url = element[1][0][0].get("href")
                except:
                    el_url = ""
                try:
                    el_upc = el_url[el_url.rfind("/upc-")+5:-1] #grab the 12 digits of the UPC code without grabbing the slash at the end
                except:
                    el_upc = ""
                try:
                    el_cat2 = element[3][0][0].text_content()
                except:
                    el_cat2 = ""
                try:
                    el_cat3 = element[5][0][0].text_content()
                except:
                    el_cat3 = ""
                try:
                    el_manufacturer = element[7][0][0].text_content()
                except:
                    el_manufacturer = ""
                el_ingredients = ""
                try:
                    ingredients = []
                    r2 = requests.get(el_url)
                    itempage = lxml.html.fromstring(r2.text)
                    for ingredient in itempage.find_class("blockNorm fontNorm")[0]:
                        try:
                            ing = ingredient.get("title")
                            if ing != None and ing != "Organic": #i dunno why this slips through, but it does
                                ingredients.append(ing)
                        except:
                            pass
                    el_ingredients = ";".join(ingredients)
                except:
                    pass
                el_weight = krogerweight(el_name,el_upc)
                if el_weight != "":
                    print el_weight
                try:
                    print el_name
                    csvfile.writerow((el_name,el_upc,el_cat1,el_cat2,el_cat3,el_manufacturer,el_url,el_ingredients,el_weight))
                except:
                    pass
            nextButton = False
            for element in cattree.find_class("tableTopBottom")[0][0][0]: #if there is no next button, exit
                try:
                    if "Next" in element[0].text_content(): #then does it say "Next"?
                        nextButton = True
                        break
                except:
                    pass
            #except:
            #    nextButton = True
            #finally:
            #    if nextButton == False:
            #        print "Quitting thread " + url
            #        break
            #   else:
            #        pagenum += 20
            #        url = "http://www.upcfoodsearch.com/products/" + name.replace("_", "-") + "/n-" + str(pagenum) + "/"
            #        print url

                              

threads = []

for aisle in categories:
    print aisle
    #download_all(aisle)
    t = threading.Thread(target=download_all,args=(aisle,))
    t.start()
    threads.append(t)

for t in threads:
    t.join()

print "Combining outputs..."

with open("all_products.csv", "w") as prodfile:
    csvfile = csv.writer(prodfile)
    csvfile.writerow(("Name","UPC","Category","SubCat1","SubCat2","Manufacturer","URL","Ingredients","Weight"))
    for subfile in glob.glob("*.csv"):
        if subfile == "all_products.csv":
            continue
        with open(subfile, "r") as subprod:
            subcsv = csv.reader(subprod)
            first = False
            for row in subcsv:
                if first == False:
                    first = True
                    continue
                csvfile.writerow(row)
