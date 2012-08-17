Mongo2Go - MongoDB for integration tests & local debugging
========

![Logo](https://raw.github.com/JohannesHoppe/Mongo2Go/master/src/mongo2go_200_200.png)

Mongo2Go is a manged wrapper around the latest MongoDB binaries. It targets **.NET 3.5.** and should work in later versions, too.  
Currently the Nuget package contains the executables of _mongo**d**_, _mongoimport_ and _mongoexport_ v2.2.0-rc1 (32bit).

Mongo2Go has two use cases:

1. Providing multiple, temporary and isolated MongoDB databases for unit tests (or to be precise: integration tests)
2. Providing a quick to set up MongoDB database for a local developer environment


Unit Test / Integration test
-------------------------------------
With each call of the static method **MongoDbRunner.Start()** a new MongoDB instance will be set up.
A free port will be used (starting with port 27018) and a corresponding data directory will be created.
The method returns an instance of MongoDbRunner, which implements IDisposable.
As soon as the MongoDbRunner is disposed (or if the Destructor is called by the GC),
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
The MongoDB Nuget package can be found at [https://nuget.org/packages/Mongo2Go/](https://nuget.org/packages/Mongo2Go/)

Search for „Mongo2Go“ in the Manage NuGet Packages dialog box or run:

    PM> Install-Package Mongo2Go

in the Package Manager Console. 


Release Notes / Known Bugs
------------------------------------------
This is the very first release of Mongo2Go.
There are still some glitches here and there.
The official MongoDB C# sharp driver uses a connection pool to increase efficiency.
This fact can create connection problems if multiple MongoDb instances are
created and killed within a short time frame.
Some Thead.Sleep methods currently target this issue.
Later versions should definitely address that problem.


Examples
--------

**Example: Integration Test (Machine.Specifications & Fluent Assertions)**

    [Subject("Integration Test")]
    public class when_using_the_inbuild_serialization : MongoIntegrationTest
    {
        static TestDocument findResult;
        
        Establish context = () =>
            {
                CreateConnection();
                collection.Insert(TestDocument.DummyData1());
            };

        Because of = () => findResult = collection.FindOneAs<TestDocument>();

        It should_return_a_result = () => findResult.ShouldNotBeNull();
        It should_hava_expected_data = () => findResult.ShouldHave().AllPropertiesBut(d => d.Id).EqualTo(TestDocument.DummyData1());
    }
	
    public class MongoIntegrationTest
    {
        internal static MongoDbRunner runner;
        internal static MongoCollection<TestDocument> collection;

        internal static void CreateConnection()
        {
            runner = MongoDbRunner.Start();
            
            MongoServer server = MongoServer.Create(runner.ConnectionString);
            MongoDatabase database = server.GetDatabase("TestDatabase");
            collection = database.GetCollection<TestDocument>("TestCollection");
        }

        Cleanup stuff = () => runner.Dispose();
    }	

More tests can be found at https://github.com/JohannesHoppe/Mongo2Go/tree/master/src/Mongo2GoTests/Runner

**Example: Exporting**

	using (MongoDbRunner runner = MongoDbRunner.StartForDebugging()) {

		MongoServer server = MongoServer.Create(runner.ConnectionString);
		MongoDatabase database = server.GetDatabase("TestDatabase");
		runner.Export("TestDatase", "TestCollection", @"..\..\App_Data\test.json");
	}

**Example: Importing (ASP.NET MVC 4 Web API)**

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
        }

        protected void Application_End()
        {
            _runner.Dispose();
        }
	}



