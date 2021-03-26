The binaries in this directory are automatically downloaded with the `MongoDownloader` tool.

In order to download the latest binary:

1. Go into the `Mongo2Go/src/MongoDownloader` directory
2. Run the downloader with `dotnet run`

* The _MongoDB Community Server_ binaries are fetched from [https://s3.amazonaws.com/downloads.mongodb.org/current.json](https://s3.amazonaws.com/downloads.mongodb.org/current.json)
  The latest production version is downloaded and extracted.
* The _MongoDB Database Tools_ archives are fetched from [https://s3.amazonaws.com/downloads.mongodb.org/tools/db/release.json](https://s3.amazonaws.com/downloads.mongodb.org/tools/db/release.json)
  The latest version is downloaded and extracted.