# Data.Dump
A C# data dump engine for easy creation of data extractions based on any dataset or poco, with a low memory footprint.
Currently Data.Dump only supports saving to SQL Server and Postgres 9.4+, but is easily extendable to support other platforms.

## What's it for?
The library grew out of the need to transparantly store large datasets, without knowing the datastructure beforehand. 
Here are some basic use cases, I'm sure you can come up with a few more.

* Dynamically storing data from various sources, be it a remote API, a CSV file, another database, or pretty much anything you can throw at it. 
* Creation of data extractions for use with BI tools
* Creation of tables for import procedures 
* Quick dump to the database of C# objects in order to examine structure and data
* Logging 
* ...

## How does it work?
Under the hood Data.Dump uses .NET's DataTables and DataSets for processing the data. While you can directly pass in your own DataTable or DataSet, the real power lies in the ability to pass in an any old object or collection thereof.

When you pass in C# objects, these will be translated to the appropriate datastructure. Simple types will automatically be resolved, however complex types will need some mapping. Depending on your configuration, nested objects or lists will automatically be translated to seperate tables in the database, with a foreign key in place. 

During processing, the data is transfered to a temptable using SQL Server's powerful bulkcopy technology. When ready, **any existing table with the same name will be dropped**, and replaced by the temptable. In this way the live table will always reflect the latest structure and data, while minimizing downtime. 

Table names are automatically resolved based on the Clr Type, but you can easily pass in your naming when necessary.

Special care has been taken to reduce the memory footprint while creating and storing datasets. Ofcourse, when you choose to pass in gigantic DataTables, you're on your own.

## How can I use it?
### Examples
#### Basic usage
Dumping a list of objects to the database is as easy as:
```c#
var repo = new SqlRepository(new SqlStore(ConnectionString));
var pocos = someService.GetEnumerable<Poco>();
repo.Save(pocos);
```
This code will generate a table called Pocos, with all primitive fields included. Nested objects will be ignored. 


To get a reference to the repo object you can also use your favourite flavor of Dependency Injection. Just implement ISqlStore, and it hook up.

#### Mapping nested objects
To create tables for objects contained within your root collection use:
```c#
repo.Save(
    nested, 
    Schema.Mapping.Models.Map<NestedPoco>()
        .SelectModel(x => x) //make sure to pass in the base model as well, or it won't get saved
        .SelectModel(x => x.Pocos) //select the objects to include
);
```
Notice this method will work on collections of objects as well as on single objects. In the last case a table with only one row will be created.

The above method will just save the data, with no relation between the tables. You can include a foreign key as such:
```c#
repo.Save(
    nested, 
    Schema.Mapping.Models.Map<NestedPoco>()
        .SelectModel(x => x)
        .SelectModel(
            x => x.Pocos,
            x => x.Id, // select the field containing the foreign key value 
            "NestedPocoId" //pass in a name for the key column; this can be a string or 
                           //a function returning a string.
                           //if no name is provided one will be generated;  
        )
);
```

#### Mapping complex or deeply nested objects
To save the data contained deeper in the object tree, you'll need to provide some extra info on where to find the root object to use for foreign key resolution.
```c#
repo.Save(
  deeplyNestedExtended.AsEnumerable(), //make sure to pass in your collections as enumerable 
  Schema.Mapping.Models.Map<RootPoco>()
    .SelectModel(x => x)  //root object of type RootPoco
    .SelectModel(
        x => x.NestedPocos, //child collection in RootPoco
        x => x.Id, //foreign key value from RootPoco
        "RootPocoId"
    )
    .SelectNestedModel(
       x => x.NestedPocos
            .Select(n => n.DeeplyNestedPocos.WithRoot(n)), //include DeeplyNestedPocos with a NestedPoco root
       x => x.Id //foreign key value from NestedPoco
    )
);
```

#### Named collections
Tables names will be autogenerated based on the Clr Type. As tables are overwritten on each save, this may lead to some undesireable effects. To avoid this issue, simply pass in a table name.
```c#
repo.Save(first, "FirstCollection"); //as string
repo.Save(second, () => "SecondCollection"); //or as a function
```

#### Mapping collections of primitive types
**NOTE:** This is no longer needed as of version 0.2.3

Currently there is no out of the box support for collections of primitive types. As these contain no valid information to use to create the columns, mapping will fail. Luckily there is an easy workaround:
```c#
repo.Save(
  pocos,
  Schema.Mapping.Models.Map<Poco>()
    .SelectModel(
        x => x.CollectionOfStrings.Select(p => new SingleValue<string>(p)),
        "PocoStrings" //optional table name always goes as second argument
        x => x.Id, 
        "PocoId"
    )
);
```
```SingleValue``` can be found in the ```Data.Dump.Schema.Mapping``` namespace. In the future this will be taken care of automatically.

#### Mapping dictionaries
Mapping dictionaries of primitive types will pose no problem, as the contained KeyValuePairs will translate nicely to a table with a Key and Value column. However, to map dictionaries of complex objects, you'll need to do some extra work. Here's an example using AutoMapper:
```c#
class DictPoco : Poco
{
    public string DictKey { get; set; }
}

var config = new MapperConfiguration(cfg =>
{
    cfg.CreateMap<Poco, DictPoco>();
});
var mapper = config.CreateMapper();
  
repo.Save(
  pocos,
  Schema.Mapping.Models.Map<ComplexPoco>()
    .SelectModel(
        x => x.DictionaryOfStringPoco.Select(
            p => mapper.Map(p.Value, new DictPoco { DictKey = p.Key })
        ),
        x => x.Id,
        "ComplexPocoId"
    )
);  
```

Please refer to the included sample project for more examples.

### Instalation
```
Install-Package Data.Dump.Sql 
```
