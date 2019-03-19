using FluentAssertions;
using Machine.Specifications;
using Mongo2Go.Helper;
using System;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
namespace Mongo2GoTests
{
    [Subject(typeof(MongodArguments))]
    public class when_null_additional_arguments_return_empty_string
    {
        private static string validAdditionalArguments;

        Because of = () => validAdditionalArguments = MongodArguments.GetValidAdditionalArguments(string.Empty, null);
        It should_be_empty_string = () => validAdditionalArguments.Should().BeEmpty();
    }

    [Subject(typeof(MongodArguments))]
    public class when_no_additional_arguments_return_empty_string
    {
        private static string validAdditionalArguments;

        Because of = () => validAdditionalArguments = MongodArguments.GetValidAdditionalArguments(string.Empty, string.Empty);
        It should_be_empty_string = () => validAdditionalArguments.Should().BeEmpty();
    }

    [Subject(typeof(MongodArguments))]
    public class when_additional_arguments_start_with_argument_separator_return_additional_arguments
    {
        private static string validAdditionalArguments;
        private const string additionalArgumentsUnderTest = " --argument_1 under_test --argument_2 under test";
        private const string expectedAdditionalArguments = " --argument_1 under_test --argument_2 under test";

        Because of = () => validAdditionalArguments = MongodArguments.GetValidAdditionalArguments(string.Empty, additionalArgumentsUnderTest);
        It should_be_expected_additional_arguments = () => validAdditionalArguments.Should().Be(expectedAdditionalArguments);
    }

    [Subject(typeof(MongodArguments))]
    public class when_additional_arguments_does_not_start_with_argument_separator_return_additional_arguments
    {
        private static string validAdditionalArguments;
        private const string additionalArgumentsUnderTest = "argument_1 under_test --argument_2 under test";
        private const string expectedAdditionalArguments = " --argument_1 under_test --argument_2 under test";

        Because of = () => validAdditionalArguments = MongodArguments.GetValidAdditionalArguments(string.Empty, additionalArgumentsUnderTest);
        It should_be_expected_additional_arguments = () => validAdditionalArguments.Should().Be(expectedAdditionalArguments);
    }

    [Subject(typeof(MongodArguments))]
    public class when_existing_arguments_and_additional_arguments_do_not_have_shared_options_return_additional_arguments
    {
        private static string validAdditionalArguments;
        private const string existingArguments = "--existing_argument1 --existing_argument2";
        private const string additionalArgumentsUnderTest = " --argument_1 under_test --argument_2 under test";
        private const string expectedAdditionalArguments = " --argument_1 under_test --argument_2 under test";

        Because of = () => validAdditionalArguments = MongodArguments.GetValidAdditionalArguments(existingArguments, additionalArgumentsUnderTest);
        It should_be_expected_additional_arguments = () => validAdditionalArguments.Should().Be(expectedAdditionalArguments);
    }

    [Subject(typeof(MongodArguments))]
    public class when_existing_arguments_and_additional_arguments_have_shared_options_throw_argument_exception
    {
        private static Exception exception;
        private const string duplicateArgument = "existing_argument2";
        private static readonly string existingArguments = $"--existing_argument1 --{duplicateArgument}";
        private static readonly string additionalArgumentsUnderTest = $" --argument_1 under_test --{duplicateArgument} argument2_new_value --argument_2 under test";

        Because of = () => exception = Catch.Exception(() => MongodArguments.GetValidAdditionalArguments(existingArguments, additionalArgumentsUnderTest));
        It should_throw_argument_exception = () => exception.Should().BeOfType<ArgumentException>();
        It should_contain_more_than_instance_of_the_duplicate_argument = () => exception.Message.IndexOf(duplicateArgument, StringComparison.InvariantCultureIgnoreCase).Should().NotBe(exception.Message.LastIndexOf(duplicateArgument, StringComparison.InvariantCultureIgnoreCase));
    }
}
// ReSharper restore UnusedMember.Local
// ReSharper restore InconsistentNaming