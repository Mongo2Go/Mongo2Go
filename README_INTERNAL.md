# Mongo2Go - Knowledge for Maintainers

## Creating a release

Mongo2Go uses [MinVer](https://github.com/adamralph/minver) for its versioning, so a tag must exist with the chosen semantic version number in order to create an official release.

1.  Create an **[annotated](https://stackoverflow.com/questions/11514075/what-is-the-difference-between-an-annotated-and-unannotated-tag/25996877#25996877)** tag, the (multi-line) message of the annotated tag will be the content of the GitHub release. Markdown can be used.

    `git tag --annotate 1.0.0-rc.1`

2.  [Push the tag](https://stackoverflow.com/questions/5195859/how-do-you-push-a-tag-to-a-remote-repository-using-git/26438076#26438076)

    `git push --follow-tags`

Once pushed, the GitHub [Continuous Integration](https://github.com/Mongo2Go/Mongo2Go/blob/master/.github/workflows/continuous-integration.yml) workflow takes care of building, running the tests, creating the NuGet package and publishing the produced NuGet package.
