using System;
using System.IO;
using QFG5Extractor.Core;

namespace QFG5Extractor.Storage.SPK
{
    public static class AssetArchiveHelper
    {
        public static string EnsureExtracted(string archiveFileName)
        {
            string archivePath = SpkConverter.FindArchiveFile(SharedState.BaseDataPath, archiveFileName);
            if (archivePath == null)
                throw new FileNotFoundException("The configured Game DATA folder does not contain " + archiveFileName + ".");

            string outputFolder = SharedState.GetArchiveModsPath(archiveFileName);
            int extractedCount = SpkConverter.ExtractMissing(archivePath, outputFolder);
            if (extractedCount > 0)
                SharedState.NotifyGameDataExtracted();
            return outputFolder;
        }
    }
}
