using System.IO;
using FluentAssertions;
using Machine.Specifications;
using FluentAssertions.Formatting;
using Mongo2Go.Helper;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
namespace Mongo2GoTests
{
    [Subject("FolderSearch")]
    public class when_requesting_current_executing_directory
    {
        public static string directory;

        Because of = () => directory = FolderSearch.CurrentExecutingDirectory();
        It should_contain_correct_path = () => directory.Should().Contain(@"Mongo2GoTests\bin");
    }

    [Subject("FolderSearch")]
    public class when_searching_for_folder : FolderSearchSpec
    {
        const string startDirectory = @"C:\test1\test2";
        const string searchPattern = @"packages\Mongo2Go*\tools\mongodb-win32-i386*\bin";
        static string directory;

        Because of = () => directory = startDirectory.FindFolder(searchPattern);
        It should_find_the_path_with_the_highest_version_number = () => directory.Should().Be(MongoBinaries);
    }

    [Subject("FolderSearch")]
    public class when_searching_for_not_existing_folder : FolderSearchSpec
    {
        const string startDirectory = @"C:\test1\test2";
        const string searchPattern = @"packages\Mongo2Go*\XXX\mongodb-win32-i386*\bin";
        static string directory;

        Because of = () => directory = startDirectory.FindFolder(searchPattern);
        It should_return_null = () => directory.Should().BeNull();
    }

    [Subject("FolderSearch")]
    public class when_searching_for_folder_upwards : FolderSearchSpec
    {
        const string searchPattern = @"packages\Mongo2Go*\tools\mongodb-win32-i386*\bin";
        static string directory;

        Because of = () => directory = LocationOfAssembly.FindFolderUpwards(searchPattern);
		It should_find_the_path_with_the_highest_version_number = () => directory.Should().Be(MongoBinaries);
    }

    [Subject("FolderSearch")]
    public class when_searching_for_not_existing_folder_upwards : FolderSearchSpec
    {
        const string searchPattern = @"packages\Mongo2Go*\XXX\mongodb-win32-i386*\bin";
        static string directory;

        Because of = () => directory = LocationOfAssembly.FindFolderUpwards(searchPattern);
        It should_return_null = () => directory.Should().BeNull();
    }

    [Subject("FolderSearch")]
    public class when_remove_last_part_of_path
    {
        static string directory;

        Because of = () => directory = @"test1\test2\test3".RemoveLastPart();
		It should_remove_the_element = () => directory.Should().Be(@"test1\test2");
    }

    [Subject("FolderSearch")]
    public class when_remove_last_part_of_single_element_path
    {
        static string directory;

        Because of = () => directory = "test1".RemoveLastPart();
        It should_return_null = () => directory.Should().BeNull();
    }

    public class FolderSearchSpec
    {
        public const string MongoBinaries = @"C:\test1\test2\packages\Mongo2Go.1.2.3\tools\mongodb-win32-i386-2.0.7-rc0\bin";
        public const string MongoOlderBinaries = @"C:\test1\test2\packages\Mongo2Go.1.1.1\tools\mongodb-win32-i386-2.0.7-rc0\bin";
        public const string LocationOfAssembly = @"C:\test1\test2\Project\bin";

        Establish context = () =>
        {
            if (!Directory.Exists(MongoBinaries)) { Directory.CreateDirectory(MongoBinaries); }
            if (!Directory.Exists(MongoOlderBinaries)) { Directory.CreateDirectory(MongoOlderBinaries); }
            if (!Directory.Exists(LocationOfAssembly)) { Directory.CreateDirectory(LocationOfAssembly); }
        };

        Cleanup stuff = () => { if (Directory.Exists(@"C:\test1")) { Directory.Delete(@"C:\test1", true); }};
    }
}
// ReSharper restore UnusedMember.Local
// ReSharper restore InconsistentNaming