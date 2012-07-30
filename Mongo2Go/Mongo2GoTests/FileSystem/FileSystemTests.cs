using System.IO;
using Machine.Specifications;
using Mongo2Go.Helper;

// ReSharper disable InconsistentNaming
namespace Mongo2GoTests.FileSystem
{
    [Subject("FileSystem")]
    public class when_requesting_current_executing_directory
    {
        public static string directory;

        Because of = () => directory = Mongo2Go.Helper.FileSystem.CurrentExecutingDirectory();
        It should_contain_correct_path = () => directory.ShouldContain(@"Mongo2GoTests\bin");
    }

    [Subject("FileSystem")]
    public class when_searching_for_folder : FolderFindingSpec
    {
        const string startDirectory = @"C:\test1\test2";
        const string searchPattern = @"packages\Mongo2Go*\Tools\mongodb-win32-i386*\bin";
        static string directory;

        Because of = () => directory = startDirectory.FindFolder(searchPattern);
        It should_find_the_path_with_the_highest_version_number = () => directory.ShouldEqual(mongoDbBinaries);
    }

    [Subject("FileSystem")]
    public class when_searching_for_not_existing_folder : FolderFindingSpec
    {
        const string startDirectory = @"C:\test1\test2";
        const string searchPattern = @"packages\Mongo2Go*\XXX\mongodb-win32-i386*\bin";
        static string directory;

        Because of = () => directory = startDirectory.FindFolder(searchPattern);
        It should_return_null = () => directory.ShouldBeNull();
    }

    [Subject("FileSystem")]
    public class when_searching_for_folder_upwards : FolderFindingSpec
    {
        const string searchPattern = @"packages\Mongo2Go*\Tools\mongodb-win32-i386*\bin";
        static string directory;

        Because of = () => directory = locationOfAssembly.FindFolderUpwards(searchPattern);
        It should_find_the_path_with_the_highest_version_number = () => directory.ShouldEqual(mongoDbBinaries);
    }

    [Subject("FileSystem")]
    public class when_searching_for_not_existing_folder_upwards : FolderFindingSpec
    {
        const string searchPattern = @"packages\Mongo2Go*\XXX\mongodb-win32-i386*\bin";
        static string directory;

        Because of = () => directory = locationOfAssembly.FindFolderUpwards(searchPattern);
        It should_return_null = () => directory.ShouldBeNull();
    }

    [Subject("FileSystem")]
    public class when_remove_last_part_of_path
    {
        static string directory;

        Because of = () => directory = @"test1\test2\test3".RemoveLastPart();
        It should_remove_the_element = () => directory.ShouldEqual(@"test1\test2");
    }

    [Subject("FileSystem")]
    public class when_remove_last_part_of_single_element_path
    {
        static string directory;

        Because of = () => directory = "test1".RemoveLastPart();
        It should_return_null = () => directory.ShouldBeNull();
    }

    public class FolderFindingSpec
    {
        public const string mongoDbBinaries = @"C:\test1\test2\packages\Mongo2Go.1.2.3\Tools\mongodb-win32-i386-2.0.7-rc0\bin";
        public const string mongoDbOlderBinaries = @"C:\test1\test2\packages\Mongo2Go.1.1.1\Tools\mongodb-win32-i386-2.0.7-rc0\bin";
        public const string locationOfAssembly = @"C:\test1\test2\Project\bin";

        Establish context = () =>
        {
            if (!Directory.Exists(mongoDbBinaries)) { Directory.CreateDirectory(mongoDbBinaries); }
            if (!Directory.Exists(mongoDbOlderBinaries)) { Directory.CreateDirectory(mongoDbOlderBinaries); }
            if (!Directory.Exists(locationOfAssembly)) { Directory.CreateDirectory(locationOfAssembly); }
        };

        Cleanup stuff = () => { if (Directory.Exists(@"C:\test1")) { Directory.Delete(@"C:\test1", true); }};
    }
}
// ReSharper restore InconsistentNaming