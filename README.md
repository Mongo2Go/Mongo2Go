Mongo2Go - MongoDB for integration tests & local debugging
========

![Logo](src/mongo2go_200_200.png)

[![Build Status](https://travis-ci.org/Mongo2Go/Mongo2Go.svg?branch=master)](https://travis-ci.org/Mongo2Go/Mongo2Go)

Mongo2Go is a managed wrapper around the latest MongoDB binaries.
It targets **.NET Standard 1.6** (and **.NET 4.6** for legacy environments) and works with Windows, Linux and macOS.
This Nuget package contains the executables of _mongod_, _mongoimport_ and _mongoexport_ **for Windows, Linux and macOS** .

__Brought to you by [Johannes Hoppe](https://haushoppe-its.de), follow him on [Twitter](https://twitter.com/johanneshoppe).__ 

Mongo2Go has two use cases:

1. Providing multiple, temporary and isolated MongoDB databases for unit tests (or to be precise: integration tests)
2. Providing a quick to set up MongoDB database for a local developer environment


Unit Test / Integration test
-------------------------------------
With each call of the static method **MongoDbRunner.Start()** a new MongoDB instance will be set up.
A free port will be used (starting with port 27018) and a corresponding data directory will be created.
The method returns an instance of MongoDbRunner, which implements IDisposable.
As soon as the MongoDbRunner is disposed (or if the Finalizer is called by the GC),
the wrapped MongoDB process will be killed and all data in the data directory will be deleted.


Local debugging
------------------------
In this mode a single MongoDB instance will be started on the default port (27017).
No data will be deleted and the MongoDB instance won’t be killed automatically.
Multiple calls to **MongoDbRunner.StartForDebugging()** will return an instance with the State “AlreadyRunning”.
You can ignore the IDisposable interface, as it won’t have any effect.
**I highly recommend to not use this mode on productive machines!**
Here you should set up a MongoDB as it is described in the manual.
For you convenience the MongoDbRunner also exposes _mongoexport_ and _mongoimport_
which allow you to quickly set up a working environment.


Installation
--------------
The Mongo2Go Nuget package can be found at [https://nuget.org/packages/Mongo2Go/](https://nuget.org/packages/Mongo2Go/)

Search for „Mongo2Go“ in the Manage NuGet Packages dialog box or run:

    PM> Install-Package Mongo2Go -Version 2.2.6

or run for the legacy **.NET 4.6** package:

    PM> Install-Package Mongo2Go -Version 1.1.0

in the Package Manager Console. 

* The new 2.x branch targets __.NET Standard 1.6__, please use the latest packages if possible. 
* The old 1.x branch targets good-old classic __.NET 4.6.1__. This is for legacy environments only. No new features will be added, only bugfixes will be made.


Examples
--------

**Example: Integration Test (Machine.Specifications & Fluent Assertions)**

```c#
[Subject("Runner Integration Test")]
public class when_using_the_inbuild_serialization : MongoIntegrationTest
{
    static TestDocument findResult;
    
    Establish context = () =>
        {
            CreateConnection();
            _collection.Insert(TestDocument.DummyData1());
        };

    Because of = () => findResult = _collection.FindOneAs<TestDocument>();

    It should_return_a_result = () => findResult.ShouldNotBeNull();
    It should_hava_expected_data = () => findResult.ShouldHave().AllPropertiesBut(d => d.Id).EqualTo(TestDocument.DummyData1());

    Cleanup stuff = () => _runner.Dispose();
}

public class MongoIntegrationTest
{
    internal static MongoDbRunner _runner;
    internal static MongoCollection<TestDocument> _collection;

    internal static void CreateConnection()
    {
        _runner = MongoDbRunner.Start();
        
        MongoServer server = MongoServer.Create(_runner.ConnectionString);
        MongoDatabase database = server.GetDatabase("IntegrationTest");
        _collection = database.GetCollection<TestDocument>("TestCollection");
    }
}    
```

More tests can be found at https://github.com/Mongo2Go/Mongo2Go/tree/master/src/Mongo2GoTests/Runner

**Example: Exporting**

```c#
using (MongoDbRunner runner = MongoDbRunner.StartForDebugging()) {

    runner.Export("TestDatase", "TestCollection", @"..\..\App_Data\test.json");
}
```

**Example: Importing (ASP.NET MVC 4 Web API)**

```c#
public class WebApiApplication : System.Web.HttpApplication
{
    private MongoDbRunner _runner;

    protected void Application_Start()
    {
        _runner = MongoDbRunner.StartForDebugging();
        _runner.Import("TestDatase", "TestCollection", @"..\..\App_Data\test.json", true);

        MongoServer server = MongoServer.Create(_runner.ConnectionString);
        MongoDatabase database = server.GetDatabase("TestDatabase");
        MongoCollection<TestObject> collection = database.GetCollection<TestObject>("TestCollection");

        /* happy coding! */
    }

    protected void Application_End()
    {
        _runner.Dispose();
    }
}
```

Changelog
-------------------------------------

### Mongo2Go 2.2.7, August 13 2018
* updates the `MongoBinaryLocator` to look for binaries in the nuget cache if they are not found in the project directory.
    * this will make Mongo2Go compatible with projects using the nuget `PackageReference` option. (PR [#56](https://github.com/Mongo2Go/Mongo2Go/pull/56), fixes [#39](https://github.com/Mongo2Go/Mongo2Go/issues/39) and [#55](https://github.com/Mongo2Go/Mongo2Go/issues/55))
* adds the `binariesSearchDirectory` parameter to `MongoDbRunner.Start` which allows an additional binaries search directory to be provided.
    * this will make the db runner more flexible if someone decides to use it in some unpredictable way.
* many thanks to [Nicholas Markkula](https://github.com/nickmkk)

### Mongo2Go 2.2.6, July 20 2018
* fixes broken linux support (fixes [#47](https://github.com/Mongo2Go/Mongo2Go/issues/47))

### Mongo2Go 2.2.5, July 19 2018
* fixes unresponsive process issue (PR [#52](https://github.com/Mongo2Go/Mongo2Go/pull/52), fixes [#49](https://github.com/Mongo2Go/Mongo2Go/issues/49))
* many thanks to [narendrachava](https://github.com/narendrachava)

### Mongo2Go 2.2.4, June 06 2018
* better support for TeamCity: removed MaxLevelOfRecursion limitation when searching for MongoDb binaries (PR [#50](https://github.com/Mongo2Go/Mongo2Go/pull/50), fixes [#39](https://github.com/Mongo2Go/Mongo2Go/issues/39))
* many thanks to [Stanko Culaja](https://github.com/culaja)

### Mongo2Go 2.2.2, June 05 2018
* includes mongod, mongoimport and mongoexport v3.6.1 for Windows, Linux and macOS via PR [#46](https://github.com/Mongo2Go/Mongo2Go/pull/46), which fixes [#45](https://github.com/Mongo2Go/Mongo2Go/issues/45)
* many thanks to [Joe Chan](https://github.com/joehmchan)

### Mongo2Go 2.2.1, November 23 2017
* no MongoDB binaries changed, still .NET Standard 1.6
* feature: uses temporary directory instead of good-old windows style `C:\data\db` by default (PR [#42](https://github.com/Mongo2Go/Mongo2Go/pull/42)) - `MongoDbRunner.Start()` and `MongoDbRunner.StartForDebugging()` will now work without any extra parameters for Linux/macOS
* bugfix: runs again on Linux/macOS, by making the binaries executable (PR [#42](https://github.com/Mongo2Go/Mongo2Go/pull/42), which fixes [#37](https://github.com/Mongo2Go/Mongo2Go/issues/37) and might also fix [#43](https://github.com/Mongo2Go/Mongo2Go/issues/43))
* internal: Unit Tests are running again (PR [#44](https://github.com/Mongo2Go/Mongo2Go/pull/44), which fixes [#31](https://github.com/Mongo2Go/Mongo2Go/issues/31), [#40](https://github.com/Mongo2Go/Mongo2Go/issues/40))
* internal: No hardcoded path passed to MongoDbRunner constructor (fixes [41](https://github.com/Mongo2Go/Mongo2Go/issues/41))
* many thanks to [Per Liedman](https://github.com/perliedman)

### Mongo2Go 2.2.0, August 17 2017
* includes mongod, mongoimport and mongoexport v3.4.7 for Windows, Linux and macOS
* targets .NET Standard 1.6 (can be used with .NET Core 1.0 / 1.1 / 2.0)
* many thanks to [Aviram Fireberger](https://github.com/avrum)

### Mongo2Go 2.1.0, March 10 2017
* skips v2.0 to have same numbers as v1.x.
* no MongoDB binaries changed since 2.0.0-alpha1 (still MongoDB v3.2.7 for Windows, Linux and macOS)
* targets .NET Standard 1.6 (can be used with .NET Core 1.0 / 1.1)
* bugfix: prevent windows firewall popup (PR [#30](https://github.com/Mongo2Go/Mongo2Go/pull/30), which fixes [#21](https://github.com/Mongo2Go/Mongo2Go/pull/21))
* many thanks to [kubal5003](https://github.com/kubal5003)

### Mongo2Go 1.1.0, March 10 2017 _(legacy branch!)_
* no MongoDB binaries changed since v1.0 (still MongoDB v3.2.7 for Windows, Linux and macOS)
* targets .NET 4.6.1
* bugfix: prevent windows firewall popup (PR [#29](https://github.com/Mongo2Go/Mongo2Go/pull/29), which fixes [#21](https://github.com/Mongo2Go/Mongo2Go/pull/21))
* many thanks to [kubal5003](https://github.com/kubal5003)


### Mongo2Go 2.0.0-alpha1, December 19 2016
* this version has no support for .NET Framework 4.6, please continue to use the stable package v.1.0.0
* NEW: first support of .NET Standard 1.6 ([#25](https://github.com/Mongo2Go/Mongo2Go/pull/25))
    * many thanks to [Hassaan Ahmed](https://github.com/bannerflow-hassaan)    
	* see the [Wiki](https://github.com/Mongo2Go/Mongo2Go/wiki/NetStandard) for more information about .NET Core 1.0 / .NET Standard 1.6


### Mongo2Go 1.0.0, November 14 2016
* v1.0 finally marked as stable
* no changes to 1.0.0-beta4
* changes since last stable version (0.2):
	* includes mongod, mongoimport and mongoexport v3.2.7 for Windows, Linux and macOS
	* support for Windows, Linux and macOS
	* uses MongoDB.Driver 2.3.0
	* **requires .NET 4.6**
	* various small bugfixes and improvements

### Mongo2Go 1.0.0-beta4, October 24 2016
* update to MongoDB.Driver 2.3.0 ([#23](https://github.com/Mongo2Go/Mongo2Go/pull/23))
* upgraded to __.NET 4.6__
* internal change: update MSpec as well and add MSTest Adapter for MSpec (ReSharper console runner doesn't support 4.6)
* many thanks to [Alexander Zeitler](https://github.com/AlexZeitler)
* please report any kind of [issues here on github](https://github.com/Mongo2Go/Mongo2Go/issues) so that we can mark 1.0.0 as stable!

### Mongo2Go 1.0.0-beta3, August 22 2016
* feature: process windows are hidden now ([#20](https://github.com/Mongo2Go/Mongo2Go/pull/20))
* bugfix: random folders are used for storing databases ([#18](https://github.com/Mongo2Go/Mongo2Go/pull/18))
* many thanks to [Matt Kocaj](https://github.com/cottsak)
* please report any kind of [issues here on github](https://github.com/Mongo2Go/Mongo2Go/issues) so that we can mark 1.0.0 as stable!

### Mongo2Go 1.0.0-beta2, July 29 2016
* fixes for bugs that were introduced by the big rewrite for cross-platform support
* changes from pull request [#14](https://github.com/Mongo2Go/Mongo2Go/pull/14), which fixes [#12](https://github.com/Mongo2Go/Mongo2Go/issues/12), [#13](https://github.com/Mongo2Go/Mongo2Go/issues/13) and [#15](https://github.com/Mongo2Go/Mongo2Go/issues/15), many thanks to [Mitch Ferrer](https://github.com/G3N7)
* please report any kind of [issues here on github](https://github.com/Mongo2Go/Mongo2Go/issues) so that we can mark 1.0.0 as stable!


### Mongo2Go 1.0.0-beta, July 24 2016
* **:tada: NEW: support for Linux and macOS :tada:**
* many thanks to [Kristofer Linnestjerna](https://github.com/krippz) from [netclean.com](http://www.netclean.com/) for the new cross-platform support
* includes mongod, mongoimport and mongoexport v3.2.7 for Windows, Linux and macOS
* changes from pull request [#8](https://github.com/Mongo2Go/Mongo2Go/pull/8), [#10](https://github.com/Mongo2Go/Mongo2Go/pull/10), [#11](https://github.com/Mongo2Go/Mongo2Go/pull/11) which fixes [#9](https://github.com/Mongo2Go/Mongo2Go/issues/9)
* please report any kind of [issues here on github](https://github.com/Mongo2Go/Mongo2Go/issues) so that we can mark 1.0.0 as stable!

### Mongo2Go 0.2, May 30 2016
* includes mongod, mongoimport and mongoexport v3.2.6,   
  (**64bit** from [win32/mongodb-win32-x86_64-2008plus-3.2.6.zip](http://downloads.mongodb.org/win32/mongodb-win32-x86_64-2008plus-3.2.6.zip?_ga=1.190428203.1815541971.1457905247) since 32bit builds are deprecated now)
* removes outmoded Strong-Name signing from assemblies (please open an issue if you really need this, see also [mspec#190](https://github.com/machine/machine.specifications/issues/190))
* changes from pull request [#7](https://github.com/Mongo2Go/Mongo2Go/pull/7), thanks to [Mitch Ferrer](https://github.com/G3N7)

### Mongo2Go 0.1.8, March 13 2016
* includes mongod, mongoimport and mongoexport v3.0.10 (32bit)
* changes from pull request [#5](https://github.com/Mongo2Go/Mongo2Go/pull/5), thanks to [Aristarkh Zagorodnikov](https://github.com/onyxmaster)

### Mongo2Go 0.1.6, July 21 2015
* includes mongod, mongoimport and mongoexport v3.0.4 (32bit)
* bug fix [#4](https://github.com/Mongo2Go/Mongo2Go/issues/4):  
Sometimes the runner tries to delete the database directory before the mongod process has been stopped, this throws an IOException. 
Now the runner waits until the mongod process has been stopped before the database directory will be deleted.  
* Thanks [Sergey Zwezdin](https://github.com/sergun)

### Mongo2Go 0.1.5, July 08 2015
* includes mongod, mongoimport and mongoexport v2.6.6 (32bit)
* changes from pull request [#3](https://github.com/Mongo2Go/Mongo2Go/pull/3)
* new: `Start` and `StartForDebugging` methods accept an optional parameter to specify a different data directory (default is "C:\data\db")
* many thanks to [Marc](https://github.com/Silv3rcircl3)

### Mongo2Go 0.1.4, January 26 2015
* includes mongod, mongoimport and mongoexport v2.6.6 (32bit)
* changes from pull request [#2](https://github.com/Mongo2Go/Mongo2Go/pull/2)
* internal updates for testing the package (not part of the release)
    * updated MSpec package so that it would work with the latest VS and R# test runner
    * updated Mongo C# Driver, Fluent Assertions, and Moq packages to latest versions
    * fixed date handling for mongoimport and mongoexport to pass tests
* many thanks to [Jesse Sweetland](https://github.com/sweetlandj) 

### Mongo2Go 0.1.3, September 20 2012
* includes mongod, mongoimport and mongoexport v2.2.0 (32bit)

### Mongo2Go 0.1.2, August 20 2012
* stable version
* includes mongod, mongoimport and mongoexport v2.2.0-rc1 (32bit)

### Mongo2Go 0.1.1, August 16 2012
* second alpha version
* includes mongod, mongoimport and mongoexport v2.2.0-rc1 (32bit)


### Mongo2Go 0.1.0, August 15 2012
* first alpha version
* includes mongod, mongoimport and mongoexport v2.2.0-rc1 (32bit)


How to contribute
-------------------------------------

Just fork the project, make your changes send us a PR.  
You can compile the project with Visual Studio 2017 and/or the [.NET Core 2.0](https://www.microsoft.com/net/core) CLI!

In the root folder, just run:
```
dotnet restore
dotnet build
dotnet test src/Mongo2GoTests
```
