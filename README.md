Mongo2Go - MongoDB for integration tests & local debugging
========

![Logo](src/mongo2go_200_200.png)

<!--[![Build Status](https://travis-ci.org/Mongo2Go/Mongo2Go.svg?branch=master)](https://travis-ci.org/Mongo2Go/Mongo2Go) Linux Build (Ubuntu Trusty)-->
 [![Build status](https://ci.appveyor.com/api/projects/status/u9mp0ceh57sdsx97/branch/master?svg=true)](https://ci.appveyor.com/project/JohannesHoppe/mongo2go-pc320/branch/master) Windows Build (Windows Server 2016)



Mongo2Go is a managed wrapper around the latest MongoDB binaries.
It targets **.NET Standard 2.1** (and **.NET 4.6** for legacy environments) and works with Windows, Linux and macOS.
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


Single server replica set mode to enable transactions 
-------------------------
`MongoDbRunner.Start()` can be set up to take in an optional boolean parameter called `singleNodeReplSet`.
When passed in with the value `true` - (**`MongoDbRunner.Start(singleNodeReplSet: true)`**)
- a single node mongod instance will be started as a replica set with the name `singleNodeReplSet`.
Replica set mode is required for transactions to work in MongoDB 4.0 or greater

Replica set initialization requires the use of a short delay to allow for the replica set to stabilize. This delay is linked to a timeout value of 5 seconds.

If the timeout expires before the replica set has stabilized a `TimeoutException` will be thrown.

The default timeout can be changed through the optional parameter `singleNodeReplSetWaitTimeout`, which allows values between 0 and 65535 seconds: **`MongoDbRunner.Start(singleNodeReplSet: true, singleNodeReplSetWaitTimeout: 10)`**

Additional mongod arguments
---------------------------
`MongoDbRunner.Start()` can be set up to consume additional `mongod` arguments. This can be done using the string parameter called `additionalMongodArguments`.

The list of additional arguments cannot contain arguments already defined internally by Mongo2Go. An `ArgumentException` will be thrown in this case, specifying which additional arguments are required to be discarded.

Example of usage of the additional `mongod` arguments: **`MongoDbRunner.Start(additionalMongodArguments: "--quiet")`**

Installation
--------------
The Mongo2Go Nuget package can be found at [https://nuget.org/packages/Mongo2Go/](https://nuget.org/packages/Mongo2Go/)

Search for „Mongo2Go“ in the Manage NuGet Packages dialog box or run:

    PM> Install-Package Mongo2Go

or run for the deprecated **.NET Standard 1.6** package:

    PM> Install-Package Mongo2Go -Version 2.2.16

or run for the legacy **.NET 4.6** package:

    PM> Install-Package Mongo2Go -Version 1.1.0

in the Package Manager Console. 

* The new 3.x branch targets __.NET Standard 2.1__. Please use this version if possible. 
* The old 2.x branch targets __.NET Standard 1.6__. No new features will be added, only bugfixes might be made.
* The old 1.x branch targets good-old classic __.NET 4.6.1__. This is for legacy environments only. No changes will be made.


Examples
--------

**Example: Integration Test (here: Machine.Specifications & Fluent Assertions)**

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
        
        MongoClient client = new MongoClient(_runner.ConnectionString);
        MongoDatabase database = client.GetDatabase("IntegrationTest");
        _collection = database.GetCollection<TestDocument>("TestCollection");
    }
}    
```

More tests can be found at https://github.com/Mongo2Go/Mongo2Go/tree/master/src/Mongo2GoTests/Runner

**Example: Exporting seed data**

```c#
using (MongoDbRunner runner = MongoDbRunner.StartForDebugging()) {

    runner.Export("TestDatabase", "TestCollection", @"..\..\App_Data\test.json");
}
```

**Example: Importing for local debugging (compatible with ASP.NET MVC 4 Web API as well as ASP.NET Core)**

```c#
public class WebApiApplication : System.Web.HttpApplication
{
    private MongoDbRunner _runner;

    protected void Application_Start()
    {
        _runner = MongoDbRunner.StartForDebugging();
        _runner.Import("TestDatabase", "TestCollection", @"..\..\App_Data\test.json", true);

        MongoClient client = new MongoClient(_runner.ConnectionString);
        MongoDatabase database = client.GetDatabase("TestDatabase");
        MongoCollection<TestObject> collection = database.GetCollection<TestObject>("TestCollection");

        /* happy coding! */
    }

    protected void Application_End()
    {
        _runner.Dispose();
    }
}
```

**Example: Transactions (New feature since v2.2.8)**

<details>
  <summary><b>Full integration test with transaction handling</b> (click to show)</summary>


```c#
 public class when_transaction_completes : MongoTransactionTest
    {
        private static TestDocument mainDocument;
        private static TestDocument dependentDocument;
        Establish context = () =>

        {
            _runner = MongoDbRunner.Start(singleNodeReplSet: true);
             client = new MongoClient(_runner.ConnectionString);
            database = client.GetDatabase(_databaseName);
            _mainCollection = database.GetCollection<TestDocument>(_mainCollectionName);
            _dependentCollection = database.GetCollection<TestDocument>(_dependentCollectionName);
            _mainCollection.InsertOne(TestDocument.DummyData2());
            _dependentCollection.InsertOne(TestDocument.DummyData2());
        };

        private Because of = () =>
        {
            var filter = Builders<TestDocument>.Filter.Where(x => x.IntTest == 23);
            var update = Builders<TestDocument>.Update.Inc(i => i.IntTest, 10);

            using (var sessionHandle = client.StartSession())
            {
                try
                {
                    var i = 0;
                    while (i < 10)
                    {
                        try
                        {
                            i++;
                            sessionHandle.StartTransaction(new TransactionOptions(
                                readConcern: ReadConcern.Local,
                                writeConcern: WriteConcern.W1)); 
                            try
                            {
                                var first = _mainCollection.UpdateOne(sessionHandle, filter, update);
                                var second = _dependentCollection.UpdateOne(sessionHandle, filter, update);
                            }
                            catch (Exception e)
                            {
                                sessionHandle.AbortTransaction();
                                throw;
                            }

                            var j = 0;
                            while (j < 10)
                            {
                                try
                                {
                                    j++;
                                    sessionHandle.CommitTransaction();
                                    break;
                                }
                                catch (MongoException e)
                                {
                                    if (e.HasErrorLabel("UnknownTransactionCommitResult"))
                                        continue;
                                    throw;
                                }
                            }
                            break;
                        }
                        catch (MongoException e)
                        {
                            if (e.HasErrorLabel("TransientTransactionError"))
                                continue;
                            throw;
                        }
                    }
                }
                catch (Exception e)
                {
                    //failed after multiple attempts so log and do what is appropriate in your case
                }
            }

             mainDocument = _mainCollection.FindSync(Builders<TestDocument>.Filter.Empty).FirstOrDefault();
             dependentDocument = _dependentCollection.FindSync(Builders<TestDocument>.Filter.Empty).FirstOrDefault();
        };
        
        It main_should_be_33 = () => mainDocument.IntTest.Should().Be(33);
        It dependent_should_be_33 = () => dependentDocument.IntTest.Should().Be(33);
        Cleanup cleanup = () => _runner.Dispose();
    }

```
</details>

**Example: Logging with `ILogger`**
<details>
    <summary><b>Wire mongod's logs at info and above levels to a custom `ILogger`</b> (click to show)</summary>

```c#
public class MongoIntegrationTest
{
    internal static MongoDbRunner _runner;

    internal static void CreateConnection()
    {
        // Create a custom logger. 
        // Replace this code with your own configuration of an ILogger.
        var provider = new ServiceCollection()
            .AddLogging(config =>
            {
                // Log to a simple console and to event logs.
                config.AddSimpleConsole();
                config.AddEventLog();
            })
            .BuildServiceProvider();
        var logger = provider.GetSerivce<ILoggerFactory>().CreateLogger("Mongo2Go");

        _runner = MongoDbRunner.Start(logger: logger);
    }
}    
```
</details>

<details>
    <summary><b>Wire mongod's logs at debug levels to a custom `ILogger`</b> (click to show)</summary>

```c#
public class MongoIntegrationTest
{
    internal static MongoDbRunner _runner;

    internal static void CreateConnection()
    {
        // Create a custom logger. 
        // Replace this code with your own configuration of an ILogger.
        var provider = new ServiceCollection()
            .AddLogging(config =>
            {
                // Mongod's D1-D2 levels are logged with Debug level.
                // D3-D5 levels are logged with Trace level.
                config.SetMinimumLevel(LogLevel.Trace);

                // Log to System.Diagnostics.Debug and to the event source.
                config.AddDebug();
                config.AddEventSourceLogger();
            })
            .BuildServiceProvider();
        var logger = provider.GetSerivce<ILoggerFactory>().CreateLogger("Mongo2Go");

        _runner = MongoDbRunner.Start(
            additionalMongodArguments: "vvvvv", // Tell mongod to output its D5 level logs
            logger: logger);
    }
}    
```
</details>

Changelog
-------------------------------------

### Mongo2Go 3.1.0, Unreleased

* replaces `--sslMode disabled` (deprecated) with `--tlsMode disabled` in command line arguments to mongod.
* adds option to inject a `Microsoft.Extensions.Logging.ILogger` to `MongoDbRunner.Start(logger)` arguments.

<details>
  <summary><b>Changelog v3.0.0 to v3.0.0</b> (click to show)</summary>
  
### Mongo2Go 3.0.0, March 26 2021

* includes MongoDB binaries of **version 4.4.4** with support for Windows, Linux and macOS
* targets **.NET Standard 2.1** (can be used with .NET Core 3.0 and .NET 5.0)

* adds new MongoDownloader tool (PR [#109](https://github.com/Mongo2Go/Mongo2Go/pull/109), fixes [#82](https://github.com/Mongo2Go/Mongo2Go/issues/82) and [#112](https://github.com/Mongo2Go/Mongo2Go/issues/112) - many thanks to [Cédric Luthi](https://github.com/0xced))
* adds support for `NUGET_PACKAGES` environment variable  (PR [#110](https://github.com/Mongo2Go/Mongo2Go/pull/110) - many thanks to [Bastian Eicher](https://github.com/bastianeicher))
</details>

<details>
  <summary><b>Changelog v2.0.0-alpha1 to v2.2.16</b> (click to show)</summary>

### Mongo2Go 2.2.16, December 13 2020

* fix for non existing starting path for binary search (PR [#107](https://github.com/Mongo2Go/Mongo2Go/pull/107), fixes [#105](https://github.com/Mongo2Go/Mongo2Go/issues/105) - many thanks to [Gurov Yury](https://github.com/kenoma))

### Mongo2Go 2.2.15, December 12 2020

* throw exception if cluster is not ready for transactions after `singleNodeReplSetWaitTimeout` (PR [#103](https://github.com/Mongo2Go/Mongo2Go/pull/103) - many thanks for the continued support by [José Mira](https://github.com/zmira))
s
### Mongo2Go 2.2.14, October 17 2020

* fixes a bug with pulling mongo binaries from wrong version (PR [#87](https://github.com/Mongo2Go/Mongo2Go/pull/87), fixes [#86](https://github.com/Mongo2Go/Mongo2Go/issues/86) - many thanks to [mihevc](https://github.com/mihevc))
* ensures transaction is ready (solves error message: `System.NotSupportedException : StartTransaction cannot determine if transactions are supported because there are no connected servers.`) (PR [#101](https://github.com/Mongo2Go/Mongo2Go/pull/101), fixes [#89](https://github.com/Mongo2Go/Mongo2Go/issues/89), [#91](https://github.com/Mongo2Go/Mongo2Go/issues/91) and [#100](https://github.com/Mongo2Go/Mongo2Go/issues/100) - many thanks to [liangshiwei](https://github.com/realLiangshiwei))

### Mongo2Go 2.2.12, September 07 2019
* performance: waits for replica set ready log message, or throws if timeout expires, instead of using `Thread.Sleep(5000)` (PR [#83](https://github.com/Mongo2Go/Mongo2Go/pull/83), fixes [#80](https://github.com/Mongo2Go/Mongo2Go/issues/80) - many thanks again to [José Mira](https://github.com/zmira))

### Mongo2Go 2.2.11, May 10 2019
* allows additional custom MongoDB arguments (PR [#69](https://github.com/Mongo2Go/Mongo2Go/pull/69), fixes [#68](https://github.com/Mongo2Go/Mongo2Go/issues/68) - many thanks to [José Mira](https://github.com/zmira))
* adds option to set port for `StartForDebugging()` (PR [#72](https://github.com/Mongo2Go/Mongo2Go/pull/72), fixes [#71](https://github.com/Mongo2Go/Mongo2Go/issues/71) - many thanks to [Danny Bies](https://github.com/dannyBies))

### Mongo2Go 2.2.9, February 04 2019
* fixes a file path issue on Linux if you run on an SDK version beyond .NET Standard 1.6 (PR [#63](https://github.com/Mongo2Go/Mongo2Go/pull/63), fixes [#62](https://github.com/Mongo2Go/Mongo2Go/issues/62) and [#61](https://github.com/Mongo2Go/Mongo2Go/issues/61)) - many thanks to [Jeroen Vannevel](https://github.com/Vannevelj))
* continuous integration runs on Linux (Travis CI) and Windows (AppVeyor) now

### Mongo2Go 2.2.8, October 12 2018
* updated MongoDB binaries to 4.0.2 to support tests leveraging transaction across different collections and databases
* updated MongoDB C# driver to 2.7.0 to be compatible with MongoDB 4.0
* adds `singleNodeReplSet` paramter to `MongoDbRunner.Start` which allows mongod instance to be started as a replica set to enable transaction support (PR [#57](https://github.com/Mongo2Go/Mongo2Go/pull/57) - many thanks to [Mahi Satyanarayana](https://github.com/gbackmania))
* fixes port lookup for UnixPortWatcher (PR [#58](https://github.com/Mongo2Go/Mongo2Go/pull/58) - many thanks to [Viktor Kolybaba](https://github.com/VikKol))

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

</details>

<details>
  <summary><b>Changelog v0.1.0 to v1.0.0</b> (click to show)</summary>

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

</details>

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
